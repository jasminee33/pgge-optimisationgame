using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FlockBehaviour : MonoBehaviour
{

    [SerializeField]
    BoxCollider2D Bounds;
    

    public float TickDuration = 1.0f;
    public float TickDurationSeparationEnemy = 0.1f;
    public float TickDurationRandom = 1.0f;

    public int BoidIncr = 1000;
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

        foreach (Flock flock in flocks)
        {
            CreateFlock(flock);
        }

        StartCoroutine(Coroutine_Flocking());
        StartCoroutine(Coroutine_Random());
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

        float speed = 3.0f;
        float separationSpeed = 2.0f;

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
