using UnityEngine;

public class Manager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

public class Meep
{
    Gene dom;
    Gene resitive;

    GameObject ob;

    int food;
    int water;

    public Meep(Gene dom_, Gene res_)
    {
        this.dom = dom_;
        this.resitive = res_;
        this.food = 100
        this.water = 100
        
    }
}

public class Gene
{
    int speed;
    int sight;
    int raceHue;
}

public class Resource
{
    int startTick;
    int value;
}