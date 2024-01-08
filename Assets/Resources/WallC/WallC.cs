using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class WallC : CogsAgent
{
    // ------------------BASIC MONOBEHAVIOR FUNCTIONS-------------------
    
    // Initialize values
    protected override void Start()
    {
        base.Start();
        AssignBasicRewards();
    }

    // For actual actions in the environment (e.g. movement, shoot laser)
    protected override void FixedUpdate() {
        base.FixedUpdate();
        
        LaserControl();
        // Movement based on DirToGo and RotateDir
        if(!IsFrozen()){
            if (!IsLaserOn()){
                rBody.AddForce(dirToGo * GetMoveSpeed(), ForceMode.VelocityChange);
            }
            transform.Rotate(rotateDir, Time.deltaTime * GetTurnSpeed());
        }

        // Debug.Log(enemy.transform.localPosition);
    }


    
    // --------------------AGENT FUNCTIONS-------------------------

    // Get relevant information from the environment to effectively learn behavior
    public override void CollectObservations(VectorSensor sensor)
    {
        // Agent velocity in x and z axis 
        var localVelocity = transform.InverseTransformDirection(rBody.velocity);
        sensor.AddObservation(localVelocity.x);
        sensor.AddObservation(localVelocity.z);

        // Time remaining
        sensor.AddObservation(timer.GetComponent<Timer>().GetTimeRemaning());  

        // Agent's current rotation
        var localRotation = transform.rotation;
        sensor.AddObservation(transform.rotation.y);

        // Agent and home base's position
        sensor.AddObservation(this.transform.localPosition);
        sensor.AddObservation(baseLocation.localPosition);

        // For each target in the environment, add: its position, whether it is being carried,
        // and whether it is in a base
        foreach (GameObject target in targets){
            sensor.AddObservation(target.transform.localPosition);
            sensor.AddObservation(target.GetComponent<Target>().GetCarried());
            sensor.AddObservation(target.GetComponent<Target>().GetInBase());
        }
        
        // If the agent is frozen
        sensor.AddObservation(IsFrozen());

        // Add observation of enemy's location
        sensor.AddObservation(enemy.transform.localPosition);
    }

    // For manual override of controls. This function will use keyboard presses to simulate output from your NN 
    public override void Heuristic(float[] actionsOut)
    {
        var discreteActionsOut = actionsOut;
        discreteActionsOut[0] = 0; //Simulated NN output 0
        discreteActionsOut[1] = 0; //....................1
        discreteActionsOut[2] = 0; //....................2
        discreteActionsOut[3] = 0; //....................3

        discreteActionsOut[4] = 0;

        if (Input.GetKey(KeyCode.UpArrow))
        {
            discreteActionsOut[0] = 1;

        }       
        if (Input.GetKey(KeyCode.DownArrow))
        {
            discreteActionsOut[0] = 2;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            discreteActionsOut[1] = 2;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            discreteActionsOut[1] = 1;
        }
        

        // Shoot
        if (Input.GetKey(KeyCode.Space)){
            discreteActionsOut[2] = 1;
        }

        // GoToNearestTarget
        if (Input.GetKey(KeyCode.A)){
            discreteActionsOut[3] = 1;
        }


        // A keypress (your choice of key) for the output for GoBackToBase();
        if (Input.GetKey(KeyCode.B)){
            discreteActionsOut[4] = 1;
        }

    }

        // What to do when an action is received (i.e. when the Brain gives the agent information about possible actions)
    public override void OnActionReceived(float[] act)
    {
        AddReward(-0.005f);
        int forwardAxis = (int)act[0]; //NN output 0

        int rotateAxis = (int) act[1]; 
        int shootAxis = (int) act[2]; 
        int goToTargetAxis = (int) act[3]; 
        int goToBaseAxis = (int) act[4]; 
        
        int goToBaseAxis = 0;

        MovePlayer(forwardAxis, rotateAxis, shootAxis, goToTargetAxis, goToBaseAxis);

    }


// ----------------------ONTRIGGER AND ONCOLLISION FUNCTIONS------------------------
    // Called when object collides with or trigger (similar to collide but without physics) other objects
    protected override void OnTriggerEnter(Collider collision)
    {
        int targetsInBase = myBase.GetComponent<HomeBase>().GetCaptured();
        int totalTargetsAcquired = targetsInBase + GetCarrying();
        base.OnTriggerEnter(collision);

        
        if (collision.gameObject.CompareTag("HomeBase") && collision.gameObject.GetComponent<HomeBase>().team == GetTeam())
        {
            // When we're in the home base and we don't have anything - we'll go out in to the field
            if (targetsInBase == 0) {
                SetReward(-1.0f);
            }
            if (targetsInBase >= (targets.Length/2)) {
                AddReward(0.5f);
                  if (IsFacingObject("enemy")) {
                      SetReward(0.6f);
                      if (IsLaserOn()) {
                          SetReward(0.8f);
                      } else {
                          AddReward(-0.1f);
                      }
                  }
            }
        } else {
            // It's not in home base (out in the field)
            if (GetCarrying() >= 3) {
                // We have more than 3 targets we're carrying
                SetReward(-0.5f);
                // Punishing it from staying in the field
                 if (IsFacingObject("base")) {
                     AddReward(0.1f);
                 } else {
                     AddReward(-0.1f);
                 }
            }
            if (GetCarrying() < 3) {
                // Stay in the field!
                AddReward(GetCarrying() * 0.1f);
                 if (IsFacingObject("target")) {
                     AddReward(0.5f);
                 } else {
                     AddReward(-0.1f);
                 }
            }
            // If we have enough to win - go back to base
            if (totalTargetsAcquired > (targets.Length / 2))  {
                // Go back to base
                SetReward(-0.5f);
            }




            // Shooting the enemy
            // If enemy is closer to you than you are to the target - shoot them
            float distanceToEnemy = Vector3.Distance(enemy.transform.localPosition, transform.localPosition);
            float distanceToNearestTarget = Vector3.Distance(GetNearestTarget().transform.localPosition, transform.localPosition);
            if (distanceToEnemy < distanceToNearestTarget) {
                // If enemy is within laser distance and we're facing the enemy, add reward, otherwise add negative reward
                
                // We're closer to the enemy
                 if (IsFacingObject("enemy")) {
                     // and we're facing the enemy 
                     AddReward(0.1f);
                     if (distanceToEnemy <= 20) {
                         // if enemy is wihtin range of laser 
                         if (IsLaserOn()) {
                             // and if laser is on we add reward - we want the laser to fire
                             SetReward(0.5f);
                         } else {
                             AddReward(-0.5f);
                         }
                      }
                 } else {
                     AddReward(-0.1f);
                     // if we're not facing the enemy & we're close to the enemy 
                 }
            }            
        }
    }

    private bool IsFacingObject(string targetObject){
        // Check if the gaze is looking at the front side of the object
        Vector3 forward = transform.forward;
        Vector3 toOther;

        if (targetObject == "enemy") {
            toOther = (enemy.transform.position - transform.position).normalized;
        } else if (targetObject == "target") {
            toOther = (GetNearestTarget().transform.position - transform.position).normalized;
        } else {
            toOther = (myBase.GetComponent<HomeBase>().transform.position - transform.position).normalized;
        }

        if(Vector3.Dot(forward, toOther) < 0.7f){
            Debug.Log("Not facing the object");
            return false;
        }

        return true;
        }

    protected override void OnCollisionEnter(Collision collision) 
    {
        base.OnCollisionEnter(collision);

        // Target is not in my base and is not being carried and I am not frozen
        if (collision.gameObject.CompareTag("Target") && collision.gameObject.GetComponent<Target>().GetInBase() != GetTeam() && collision.gameObject.GetComponent<Target>().GetCarried() == 0 && !IsFrozen())
        {
            // Add rewards here
            // When you're about to carry the target 
            SetReward(1f);
        }

        if (collision.gameObject.CompareTag("Wall") && collision.gameObject.GetComponent<Target>().GetInBase() != GetTeam())
        // If you hit a wall and it's not your home base - add a negative reward 
        {
            //Add rewards here
            AddReward(-0.5f);
        }
    }



    //  --------------------------HELPERS---------------------------- 
     private void AssignBasicRewards() {
        rewardDict = new Dictionary<string, float>();

        rewardDict.Add("frozen", 0f);
        rewardDict.Add("shooting-laser", 0f);
        rewardDict.Add("hit-enemy", 0f);
        rewardDict.Add("dropped-one-target", 0f);
        rewardDict.Add("dropped-targets", 0f);
    }
    
    private void MovePlayer(int forwardAxis, int rotateAxis, int shootAxis, int goToTargetAxis, int goToBaseAxis)
    {
        dirToGo = Vector3.zero;
        rotateDir = Vector3.zero;

        Vector3 forward = transform.forward;
        Vector3 backward = -transform.forward;
        Vector3 right = transform.up;
        Vector3 left = -transform.up;

        //fowardAxis: 
            // 0 -> do nothing
            // 1 -> go forward
            // 2 -> go backward
        if (forwardAxis == 0){
            // Do nothing. This case is not necessary to include, it's only here to explicitly show what happens in case 0
        }
        else if (forwardAxis == 1){
            dirToGo = forward;
        }
        else if (forwardAxis == 2){
            dirToGo = backward;
        }

        //rotateAxis: 
            // 0 -> do nothing
            // 1 -> go right
            // 2 -> go left
        if (rotateAxis == 0){
            // Do nothing
        }
        
        if (rotateAxis == 1) {
            rotateDir = right;
        }

        if (rotateAxis == 2) {
            rotateDir = left;
        }

        // Shoot
        if (shootAxis == 1){
        SetLaser(true);
        }
        else {
        SetLaser(false);
        }

        // Go to the nearest target
        if (goToTargetAxis == 1){
            GoToNearestTarget();
        }

        // The case for goToBaseAxis
        if (goToBaseAxis == 1) {
            GoToBase();
        }
        
    }

    // Go to home base
    private void GoToBase(){
        TurnAndGo(GetYAngle(myBase));
    }

    // Go to the nearest target
    private void GoToNearestTarget(){
        GameObject target = GetNearestTarget();
        if (target != null){
            float rotation = GetYAngle(target);
            TurnAndGo(rotation);
        }        
    }

    // Rotate and go in specified direction
    private void TurnAndGo(float rotation){

        if(rotation < -5f){
            rotateDir = transform.up;
        }
        else if (rotation > 5f){
            rotateDir = -transform.up;
        }
        else {
            dirToGo = transform.forward;
        }
    }

    // Return reference to nearest target
    protected GameObject GetNearestTarget(){
        float distance = 200;
        GameObject nearestTarget = null;
        foreach (var target in targets)
        {
            float currentDistance = Vector3.Distance(target.transform.localPosition, transform.localPosition);
            if (currentDistance < distance && target.GetComponent<Target>().GetCarried() == 0 && target.GetComponent<Target>().GetInBase() != team){
                distance = currentDistance;
                nearestTarget = target;
            }
        }
        return nearestTarget;
    }

    private float GetYAngle(GameObject target) {
        
       Vector3 targetDir = target.transform.position - transform.position;
       Vector3 forward = transform.forward;

      float angle = Vector3.SignedAngle(targetDir, forward, Vector3.up);
      return angle; 
        
    }

}
