using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FlockBehaviour : MonoBehaviour
{
    List<Obstacle> mObstacles = new List<Obstacle>();

    [SerializeField]
    GameObject[] Obstacles;

    [SerializeField]
    BoxCollider2D Bounds;

    public float TickDuration = 1.0f;
    public float TickDurationSeparationEnemy = 0.1f;
    public float TickDurationRandom = 1.0f;

    public int BoidIncr = 100;
    public bool useFlocking = false;
    public int BatchSize = 100;

    public List<Flock> flocks = new List<Flock>();
    void Reset()
    {
        flocks = new List<Flock>()
        {
            new Flock()
        };
    }

    void Start()
    {
        // Randomize obstacles placement.
        for (int i = 0; i < Obstacles.Length; ++i)
        {
            float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);
            Obstacles[i].transform.position = new Vector3(x, y, 0.0f);
            Obstacle obs = Obstacles[i].AddComponent<Obstacle>();
            Autonomous autono = Obstacles[i].AddComponent<Autonomous>();
            autono.MaxSpeed = 1.0f;
            obs.mCollider = Obstacles[i].GetComponent<CircleCollider2D>();
            mObstacles.Add(obs);
        }

        foreach (Flock flock in flocks)
        {
            CreateFlock(flock);
        }

        StartCoroutine(Coroutine_Flocking());
        StartCoroutine(Coroutine_Random());
        StartCoroutine(Coroutine_AvoidObstacles());
        StartCoroutine(Coroutine_SeparationWithEnemies());
        StartCoroutine(Coroutine_Random_Motion_Obstacles());
    }

    void CreateFlock(Flock flock)
    {
        for (int i = 0; i < flock.numBoids; ++i)
        {
            float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);

            AddBoid(x, y, flock);
        }
    }

    void Update()
    {
        HandleInputs();
        Rule_CrossBorder();
        Rule_CrossBorder_Obstacles();
    }

    void HandleInputs()
    {
        if (EventSystem.current.IsPointerOverGameObject() ||
           enabled == false)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            AddBoids(BoidIncr);
        }
    }

    void AddBoids(int count)
    {
        for (int i = 0; i < count; ++i)
        {
            float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);

            AddBoid(x, y, flocks[0]);
        }
        flocks[0].numBoids += count;
    }

    void AddBoid(float x, float y, Flock flock)
    {
        GameObject obj = Instantiate(flock.PrefabBoid);
        obj.name = "Boid_" + flock.name + "_" + flock.mAutonomous.Count;
        obj.transform.position = new Vector3(x, y, 0.0f);
        Autonomous boid = obj.GetComponent<Autonomous>();
        flock.mAutonomous.Add(boid);
        boid.MaxSpeed = flock.maxSpeed;
        boid.RotationSpeed = flock.maxRotationSpeed;
    }

    static float Distance(Autonomous a1, Autonomous a2)
    {
        return (a1.transform.position - a2.transform.position).magnitude;
    }

    void Execute(Flock flock, int i)
    {
        Vector3 flockDir = Vector3.zero;
        Vector3 separationDir = Vector3.zero;
        Vector3 cohesionDir = Vector3.zero;

        float speed = 0.0f;
        float separationSpeed = 0.0f;

        int count = 0;
        int separationCount = 0;
        Vector3 steerPos = Vector3.zero;

        Autonomous curr = flock.mAutonomous[i];
        for (int j = 0; j < flock.numBoids; ++j)
        {
            Autonomous other = flock.mAutonomous[j];
            float dist = (curr.transform.position - other.transform.position).magnitude;
            if (i != j && dist < flock.visibility)
            {
                speed += other.Speed;
                flockDir += other.TargetDirection;
                steerPos += other.transform.position;
                count++;
            }
            if (i != j)
            {
                if (dist < flock.separationDistance)
                {
                    Vector3 targetDirection = (
                      curr.transform.position -
                      other.transform.position).normalized;

                    separationDir += targetDirection;
                    separationSpeed += dist * flock.weightSeparation;
                }
            }
        }
        if (count > 0)
        {
            speed = speed / count;
            flockDir = flockDir / count;
            flockDir.Normalize();

            steerPos = steerPos / count;
        }

        if (separationCount > 0)
        {
            separationSpeed = separationSpeed / count;
            separationDir = separationDir / separationSpeed;
            separationDir.Normalize();
        }

        curr.TargetDirection =
          flockDir * speed * (flock.useAlignmentRule ? flock.weightAlignment : 0.0f) +
          separationDir * separationSpeed * (flock.useSeparationRule ? flock.weightSeparation : 0.0f) +
          (steerPos - curr.transform.position) * (flock.useCohesionRule ? flock.weightCohesion : 0.0f);
    }

    //<-- removed unneccessary nested loops to make the coroutine faster  -->
    IEnumerator Coroutine_Flocking()
    {
        WaitForSeconds wait = new WaitForSeconds(0.1f); 

        while (true)
        {
            if (useFlocking)
            {
                foreach (Flock flock in flocks)
                {
                    List<Autonomous> autonomousList = flock.mAutonomous;

                    for (int i = 0; i < autonomousList.Count; i += BatchSize) //initialise loop; increases the batch size 
                    {
                        int endIndex = Mathf.Min(i + BatchSize, autonomousList.Count); //calculates end index to make sure teh end index
                                                                                       //does not go over the total count of autonomous

                        for (int j = i; j < endIndex; j++) //loops through batch;initialise and make sure j is less than endindex;update j and gets added by 1
                        {
                            Execute(flock, j); //calls excute function
                        }

                        yield return null;
                    }
                }
            }

            yield return wait;
        }
    }



    void SeparationWithEnemies_Internal(
      List<Autonomous> boids,
      List<Autonomous> enemies,
      float sepDist,
      float sepWeight)
    {
        for (int i = 0; i < boids.Count; ++i)
        {
            for (int j = 0; j < enemies.Count; ++j)
            {
                float dist = (
                  enemies[j].transform.position -
                  boids[i].transform.position).magnitude;
                if (dist < sepDist)
                {
                    Vector3 targetDirection = (
                      boids[i].transform.position -
                      enemies[j].transform.position).normalized;

                    boids[i].TargetDirection += targetDirection;
                    boids[i].TargetDirection.Normalize();

                    boids[i].TargetSpeed += dist * sepWeight;
                    boids[i].TargetSpeed /= 2.0f;
                }
            }
        }
    }

    //<-- removed unneccessary nested loops to make the coroutine faster  -->
    IEnumerator Coroutine_SeparationWithEnemies()
    {
        WaitForSeconds wait = new WaitForSeconds(0.1f); 

        while (true)
        {
            foreach (Flock flock in flocks)
            {
                if (!flock.useFleeOnSightEnemyRule || flock.isPredator)  //check if current flock is not using FleeOnSightEnemyRule or is a predator
                    continue; //used continue statement;continue to go through the loops

                foreach (Flock enemies in flocks) //goes through each flock in list
                {
                    if (!enemies.isPredator) //if the enemy is not predator 
                        continue;//continue rest of the code
                    //if continue is not used, this function is called;to control the separation btween flock and enemy
                    SeparationWithEnemies_Internal(flock.mAutonomous, enemies.mAutonomous, flock.enemySeparationDistance, flock.weightFleeOnSightEnemy);
                }
            }

            yield return wait;
        }
    }

    //<-- removed unneccessary nested loops to make the coroutine faster  -->
    IEnumerator Coroutine_AvoidObstacles()
    {
        WaitForSeconds wait = new WaitForSeconds(0.1f); 

        while (true)
        {
            foreach (Flock flock in flocks)
            {
                //create a conditional statement; checks if the rule is false, if false then the loops will start
                //continue statement ref: https://www.geeksforgeeks.org/c-sharp-continue-statement/
                if (!flock.useAvoidObstaclesRule)
                    continue;

                List<Autonomous> autonomousList = flock.mAutonomous;

                foreach (Autonomous autonomous in autonomousList) //loops through each autonomous in the list
                {
                    foreach (Obstacle obstacle in mObstacles) //loops throuch each obstacles
                    {
                        //calculate dist between current obstacle and autonomous using position - magnituate of vector 
                        float dist = (obstacle.transform.position - autonomous.transform.position).magnitude;

                        //checks if the dist is less than the avoidance radius; the autonomous is within radius and must avoid
                        if (dist < obstacle.AvoidanceRadius)
                        {
                            //calculate target direction that is away from the obstacle
                            Vector3 targetDirection = (autonomous.transform.position - obstacle.transform.position).normalized;

                            //updates the direction by adding the direction wanted multiplied bu the avoidance weight;make the autonomous go to direction to avoid
                            autonomous.TargetDirection += targetDirection * flock.weightAvoidObstacles;

                            //normalize target direction
                            autonomous.TargetDirection.Normalize();
                        }
                    }
                }
            }

            yield return wait;
        }
    }

    //<-- removed unneccessary nested loops to make the coroutine faster  -->
    IEnumerator Coroutine_Random_Motion_Obstacles()
    {
        WaitForSeconds wait = new WaitForSeconds(2.0f);

        while (true)
        {
            for (int i = 0; i < Obstacles.Length; ++i)
            {
                Autonomous autono = Obstacles[i].GetComponent<Autonomous>();

                float rand = Random.Range(0.0f, 1.0f);
                autono.TargetDirection.Normalize();
                float angle = Mathf.Atan2(autono.TargetDirection.y, autono.TargetDirection.x);

                angle += (rand > 0.5f) ? Mathf.Deg2Rad * 45.0f : -Mathf.Deg2Rad * 45.0f; //adjust the angle from the random value
                                                                                         //if its greater then 0.5, adds 45 deg else subtracts

                Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle));
                autono.TargetDirection += dir * 0.1f;
                autono.TargetDirection.Normalize();

                float speed = Random.Range(1.0f, autono.MaxSpeed);
                autono.TargetSpeed += speed;
                autono.TargetSpeed /= 2.0f;
            }

            yield return wait;
        }
    }

    //<-- removed unneccessary nested loops to make the coroutine faster  -->
    IEnumerator Coroutine_Random()
    {
        WaitForSeconds wait = new WaitForSeconds(TickDurationRandom);  //create a 

        while (true)
        {
            foreach (Flock flock in flocks)
            {
                if (flock.useRandomRule)
                {
                    List<Autonomous> autonomousList = flock.mAutonomous;
                    for (int i = 0; i < autonomousList.Count; ++i)
                    {
                        float rand = Random.Range(0.0f, 1.0f);
                        autonomousList[i].TargetDirection.Normalize();
                        float angle = Mathf.Atan2(autonomousList[i].TargetDirection.y, autonomousList[i].TargetDirection.x);

                        //used ternary operator ref: https://www.geeksforgeeks.org/conditional-or-ternary-operator-in-c/
                        angle += (rand > 0.5f) ? Mathf.Deg2Rad * 45.0f : -Mathf.Deg2Rad * 45.0f; //adjust the angle from the random value
                                                                                                 //if its greater then 0.5, adds 45 deg else subtracts
                        Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle));

                        autonomousList[i].TargetDirection += dir * flock.weightRandom;
                        autonomousList[i].TargetDirection.Normalize();

                        float speed = Random.Range(1.0f, autonomousList[i].MaxSpeed);
                        autonomousList[i].TargetSpeed += speed * flock.weightSeparation;
                        autonomousList[i].TargetSpeed /= 2.0f;
                    }
                }
            }

            yield return wait;
        }
    }

    void Rule_CrossBorder_Obstacles()
    {
        for (int i = 0; i < Obstacles.Length; ++i)
        {
            Autonomous autono = Obstacles[i].GetComponent<Autonomous>();
            Vector3 pos = autono.transform.position;
            if (autono.transform.position.x > Bounds.bounds.max.x)
            {
                pos.x = Bounds.bounds.min.x;
            }
            if (autono.transform.position.x < Bounds.bounds.min.x)
            {
                pos.x = Bounds.bounds.max.x;
            }
            if (autono.transform.position.y > Bounds.bounds.max.y)
            {
                pos.y = Bounds.bounds.min.y;
            }
            if (autono.transform.position.y < Bounds.bounds.min.y)
            {
                pos.y = Bounds.bounds.max.y;
            }
            autono.transform.position = pos;
        }

        for (int i = 0; i < Obstacles.Length; ++i)
        {
            Autonomous autono = Obstacles[i].GetComponent<Autonomous>();
            Vector3 pos = autono.transform.position;
            if (autono.transform.position.x + 5.0f > Bounds.bounds.max.x)
            {
                autono.TargetDirection.x = -1.0f;
            }
            if (autono.transform.position.x - 5.0f < Bounds.bounds.min.x)
            {
                autono.TargetDirection.x = 1.0f;
            }
            if (autono.transform.position.y + 5.0f > Bounds.bounds.max.y)
            {
                autono.TargetDirection.y = -1.0f;
            }
            if (autono.transform.position.y - 5.0f < Bounds.bounds.min.y)
            {
                autono.TargetDirection.y = 1.0f;
            }
            autono.TargetDirection.Normalize();
        }
    }


    //<--amended the Rule_CrossBorder() -->
    void Rule_CrossBorder() 
    {
        //go through the loop in the flock list
        foreach (Flock flock in flocks)
        {
            //gets list of autonomous in current flock
            List<Autonomous> autonomousList = flock.mAutonomous;
            
            float borderOffset = 5.0f; //set the border offsets

            //go through loop that  intilialise i to 0; check if i is true; adds 1
            for (int i = 0; i < autonomousList.Count; ++i)
            {
                //get the position in the transform of the autonomous 
                Vector3 pos = autonomousList[i].transform.position;

                //checks if flock bounces off the wall
                if (flock.bounceWall)
                {
                    //calls function - 'CheckBorder' to control the boundary of the x-axis 
                    CheckBorder(ref pos.x, Bounds.bounds.min.x, Bounds.bounds.max.x, borderOffset, ref autonomousList[i].TargetDirection.x);
                    //calls function - 'CheckBorder' to control the boundary of the y-axis 
                    CheckBorder(ref pos.y, Bounds.bounds.min.y, Bounds.bounds.max.y, borderOffset, ref autonomousList[i].TargetDirection.y);
                }
                else //if the flock doesnt bounce off the wall
                {
                    //boundary for the x-axis to prevent it going beyond the wall
                    OutsideBorder(ref pos.x, Bounds.bounds.min.x, Bounds.bounds.max.x);

                    //boundary for the y-axis to prevent it going beyond the wall
                    OutsideBorder(ref pos.y, Bounds.bounds.min.y, Bounds.bounds.max.y);
                }

                //update the position ofautonomous
                autonomousList[i].transform.position = pos;

                //normalises vector to make sure that the autonomous will move same direction
                autonomousList[i].TargetDirection.Normalize();
            }
        }
    }

    //bouncing at the borders; make sure axis stays in range
    void CheckBorder(ref float axis, float minValue, float maxValue, float offset, ref float targetDirection)
    {
        if (axis + offset > maxValue) //checks if axis + offset goes over max. value
        {
            targetDirection = -1.0f; //change opposite direction of target direction
            axis = maxValue - offset; //set axis to max. value minus offset
        }
        else if (axis - offset < minValue) //check if axis - offset goes below min.value
        {
            targetDirection = 1.0f; //change opposite direction of target direction 
            axis = minValue + offset; //set axis to min value plus offset
        }
    }

    //creates the axis to ake sure it is within the range 
    void OutsideBorder(ref float axis, float minValue, float maxValue)
    {
        if (axis > maxValue) //// If axis go over max value
        {
            axis = minValue; //sets to min value
        }
        else if (axis < minValue) //// If axis goes below min value
        {
            axis = maxValue; //sets to max value
        }
    }



}
