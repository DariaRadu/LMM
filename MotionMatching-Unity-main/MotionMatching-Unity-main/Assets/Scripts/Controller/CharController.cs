using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharController : MonoBehaviour
{
    private CharacterController controller;
    private Vector3 playerVelocity;
    private bool groundedPlayer;
    private Trajectory trajectory;
    private MotionTrajectoryData currentTrajectoryData = new MotionTrajectoryData();
    private MotionTrajectoryData futureTrajectoryData20 = new MotionTrajectoryData();
    private MotionTrajectoryData futureTrajectoryData40 = new MotionTrajectoryData();
    private MotionTrajectoryData futureTrajectoryData60 = new MotionTrajectoryData();
    private float timeElapsed = 0.0f;
    private int checkedFrame = 0;
    private int currentFrame = 0;
    private float frameTime = 0.0f;
    private Vector3 prevHipPosition;

    [SerializeReference]
    private float gravityValue = -9.81f;
    private int frameRate = 15;


    // Motion Matching
    MotionMatcher motionMatcher;

    private void Start()
    {
        controller = gameObject.GetComponent<CharacterController>();
        trajectory = gameObject.GetComponent<Trajectory>();

        Transform hips = transform;
        Vector3 projectedPosition = new Vector3(hips.transform.position.x, 0, hips.transform.position.z);
        Vector3 localProjectedPos = new Vector3(hips.transform.localPosition.x, 0, hips.transform.localPosition.z);

        currentTrajectoryData.position = projectedPosition;
        currentTrajectoryData.localPosition = localProjectedPos;
        currentTrajectoryData.velocity = Vector3.zero;

        // Motion Matching
        motionMatcher = gameObject.GetComponent<MotionMatcher>();
    }

    void Update()
    {
        Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

        float speed = currentTrajectoryData.velocity.magnitude;
        Vector3 move = speed * currentTrajectoryData.direction;
        controller.Move(move * Time.deltaTime);

        Transform hips = transform;
        Vector3 projectedPosition = new Vector3(hips.transform.position.x, 0, hips.transform.position.z);
        Vector3 localProjectedPos = new Vector3(hips.transform.localPosition.x, 0, hips.transform.localPosition.z);

        currentTrajectoryData.position = projectedPosition;
        currentTrajectoryData.localPosition = localProjectedPos;

        frameTime += Time.deltaTime;
        if (frameTime >= 1.0 / frameRate)
        {
            frameTime = 0;
            motionMatcher.PlayAnim(currentFrame);
            currentFrame++;
            checkedFrame++;
        }

        // Calculate current and future trajectories
        if (input != Vector3.zero)
        {
            gameObject.transform.forward = input;
            timeElapsed += Time.deltaTime;

            currentTrajectoryData.desiredDirection = gameObject.transform.forward;
            MotionTrajectoryData newTrajectoryData = trajectory.Step(currentTrajectoryData, Time.deltaTime, 1, timeElapsed);
            currentTrajectoryData.velocity = newTrajectoryData.velocity;
            currentTrajectoryData.direction = newTrajectoryData.direction;

            MotionTrajectoryData previous = currentTrajectoryData;
            MotionTrajectoryData future = currentTrajectoryData;
            for (int i = 0; i < 61; i++)
            {
                future.desiredDirection = gameObject.transform.forward;
                future = trajectory.Step(previous, Time.deltaTime, 1, timeElapsed + i * Time.deltaTime);
                previous = future;

                if (i == 20) futureTrajectoryData20 = future;
                if (i == 40) futureTrajectoryData40 = future;
            }

            futureTrajectoryData60 = future;

        }
        else
        {
            timeElapsed = 0.0f;
            currentTrajectoryData.velocity = Vector3.zero;
        }

        playerVelocity.y += gravityValue * Time.deltaTime;

        // Create query vector

        if (checkedFrame == 4) 
        {
            prevHipPosition = GameObject.Find("inputHips").transform.position;
        }

        if (checkedFrame == 5)
        {
            checkedFrame = 0;

            Feature queryVector = CalculateQueryVector();
            int bestIndex = motionMatcher.CalculateBestCandidateIndex(queryVector);
            currentFrame = bestIndex;
        }
    }

    private Feature CalculateQueryVector() {
        Feature queryVector = new Feature();
        queryVector.trajectory = currentTrajectoryData;
        queryVector.futureTrajectories = new List<MotionTrajectoryData> { futureTrajectoryData20,
                                                                              futureTrajectoryData40,
                                                                              futureTrajectoryData60 };

        queryVector.leftFootPosition = GameObject.Find("inputLeftFoot").transform.localPosition;
        queryVector.rightFootPosition = GameObject.Find("inputRightFoot").transform.localPosition;
        queryVector.rightFootPosition = GameObject.Find("inputRightFoot").transform.localPosition;

        Vector3 hipVelocity = (GameObject.Find("inputHips").transform.position - prevHipPosition) / Time.deltaTime;
        queryVector.hipVelocity = hipVelocity;
        return queryVector;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(currentTrajectoryData.position, 1);
        Gizmos.DrawWireSphere(futureTrajectoryData20.position, 2);
        Gizmos.DrawWireSphere(futureTrajectoryData40.position, 2);
        Gizmos.DrawWireSphere(futureTrajectoryData60.position, 2);

        Gizmos.color = Color.red;

        if (!motionMatcher) return;
        Feature currentFeatureVector = motionMatcher.MatchingDB[currentFrame];
        
        foreach (MotionTrajectoryData trajectory in currentFeatureVector.futureTrajectories)
        {
            Gizmos.DrawWireSphere(trajectory.position + currentTrajectoryData.position, 1);
        }
    }
}