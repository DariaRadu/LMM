import tqdm
from torch import nn
import torch as t
from torch.utils.data import DataLoader
import utils

class Decompressor(nn.Module):
    def __init__(self, input_size, output_size, latent_size=512, learning_rate=1e-3, mdb_size=30):
        super().__init__()
        self.latent_size = latent_size
        self.half_latent_size = int(latent_size/2)
        self.mdb_size = mdb_size
        self.compressor = nn.Sequential(
            nn.Linear(input_size, self.half_latent_size),
            nn.ELU(),
            nn.Linear(self.half_latent_size, int(self.half_latent_size / 2)),
            nn.ELU(),
            nn.Linear(int(self.half_latent_size / 2), int(self.half_latent_size / 2)),
            nn.ELU(),
            nn.Linear(int(self.half_latent_size / 2), self.half_latent_size),
            nn.ELU(),
            nn.Linear(self.half_latent_size, latent_size),
        )

        self.decompressor = nn.Sequential(
            nn.Linear(latent_size + mdb_size, self.half_latent_size),
            nn.ReLU(),
            nn.Linear(self.half_latent_size, self.half_latent_size),
            nn.ReLU(),
            nn.Linear(self.half_latent_size, output_size),
        )

        self.optimizer = t.optim.Adam(self.parameters(), lr=learning_rate)
        self.device = "cuda" if t.cuda.is_available() else "cpu"
        self.to(self.device)
        self.train_writer, self.val_writer = utils.get_writers("animations")
        self.batch_idx = 0
        self.epochs = 1000
        self.epoch_idx = 0

    def forward(self, x, matching_db):
        z = self.compressor(x)
        x_z = t.cat((z, matching_db), 0)

        x = self.decompressor(x_z)
        return x

    def eval_batch(self, val_data, matching_db_val):
        self.eval()
        val_loss = 0
        for (_, in_batch) in enumerate(val_data):
            in_batch = next(iter(val_data))
            in_batch_mdb = next(iter(matching_db_val))
            out_batch = in_batch
            in_batch_dev = in_batch[0].float().to(self.device)
            in_batch_mdb_dev = in_batch_mdb[0].float().to(self.device)
            out_batch_dev = out_batch[0].float().to(self.device)
            with t.no_grad():
                frames = self.forward(in_batch_dev, in_batch_mdb_dev)
                loss = t.nn.functional.mse_loss(frames, out_batch_dev)
            val_loss += loss.item()

        val_loss = val_loss / len(val_data)
        return val_loss

    def do_train(self, train_data: DataLoader, n_train_batches: int, val_data: DataLoader,
                 matching_db_train: DataLoader, matching_db_val: DataLoader,
                 val_every_n_train_batches=10):
        train_loss = []
        val_loss = []
        for epoch in range(self.epochs):
            epoch_loss = 0
            self.batch_idx = 0
            data_iter_mdb = iter(matching_db_train)
            for (_, in_batch) in enumerate(train_data):
                in_batch_mdb = next(data_iter_mdb)
                in_batch_dev = in_batch[0].float().to(self.device)
                in_batch_mdb_dev = in_batch_mdb[0].float().to(self.device)
                out_batch_dev = in_batch_dev
                self.optimizer.zero_grad()
                frames = self.forward(in_batch_dev, in_batch_mdb_dev)
                loss = t.nn.functional.mse_loss(frames, out_batch_dev)
                assert t.isfinite(loss), "loss is not finite: %f" % loss
                loss.backward()
                self.optimizer.step()
                epoch_loss += loss.item()
                self.batch_idx += 1

            loss = epoch_loss / len(train_data)
            train_loss.append(loss)
            self.train_writer.add_scalar("loss", loss, self.epoch_idx)
            self.epoch_idx += 1
            print('Epoch {} of {}, Train Loss: {:.3f}'.format(
                epoch + 1, self.epochs, loss))
            if self.epoch_idx % val_every_n_train_batches == 0:
                loss_val = self.eval_batch(val_data, matching_db_val)
                val_loss.append(loss_val)
                self.val_writer.add_scalar("loss", loss_val, self.epoch_idx)
                print('Epoch {} of {}, Validation Loss: {:.3f}'.format(
                    epoch + 1, self.epochs, loss_val))

    def save(self, fn):
        t.save({
            'batch_idx': self.batch_idx,
            'model_state_dict': self.state_dict(),
            'optimizer_state_dict': self.optimizer.state_dict(),
        }, fn)

    def load(self, fn):
        checkpoint = t.load(fn, map_location=t.device(self.device))
        self.load_state_dict(checkpoint["model_state_dict"])
        self.optimizer.load_state_dict(checkpoint["optimizer_state_dict"])
        self.batch_idx = checkpoint["batch_idx"]