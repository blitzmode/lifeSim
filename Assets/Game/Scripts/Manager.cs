
using System.Collections.Generic;
using UnityEngine;
using SQLite4Unity3d;

public class Manager : MonoBehaviour
{
    public Mesh MeepMesh;
    public Mesh ResourceMesh;
    List<Meep> meeps = new List<Meep>();
    List<Resource> resources = new List<Resource>();
    int maxReasources = 1000;
    int startingMeeps = 1000;
    int startingReasources = 100000;

    SQLiteConnection db;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        string path = Application.persistentDataPath + "saves/save1.db";
        db = new SQLiteConnection(path, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite);
        db.CreateTable<Meep>();

        for (int i = 0; i < startingMeeps; i++)
        {
            Gene_Meep dom = new Gene_Meep();
            Gene_Meep res = new Gene_Meep();
            Meep newMeep = new Meep(dom, res, null, MeepMesh);
            Vector2 ran = Random.insideUnitCircle * 30f;
            newMeep.ob.transform.position = new Vector3(ran.x, 0, ran.y);
            meeps.Add(
                newMeep
            );
        }
        for (int i = 0; i < startingReasources; i++)
        {
            Gene_Resource gene = new Gene_Resource();
            resources.Add(
                new Resource(gene, null, ResourceMesh)
            );
        }
    }
    // Update is called once per frame
    void Update()
    {
        for (int i = 0; i < meeps.Count;)
        {
            Meep meep = meeps[i];

            meep.Water -= 0;
            if (meep.Water <= 0)
            {
                Destroy(meep.ob);
                meeps.Remove(meep);
                continue;
            }

            meep.Food -= 1;
            if (meep.Food <= 0)
            {
                Destroy(meep.ob);
                meeps.Remove(meep);
                continue;
            }

            if (meep.Food >= meep.dom.minNeededForChild)
            {
                Meep bestM = null;
                float scoreM = float.MaxValue;
                for (int k = 0; k < meeps.Count; k++)
                {
                    if (i == k) continue;
                    Meep OtherMeep = meeps[k];
                    if (meep.Food >= meep.dom.minNeededForChild && OtherMeep.Food > OtherMeep.dom.minNeededForChild)
                    {
                        float distance = (meep.ob.transform.position - OtherMeep.ob.transform.position).magnitude;
                        if (distance > meep.dom.sight) continue;
                        if (distance < scoreM)
                        {
                            bestM = OtherMeep;
                            scoreM = distance;
                        }
                    }
                }
                if (bestM != null)
                {
                    Vector3 dir = bestM.ob.transform.position - meep.ob.transform.position;
                    if (dir.magnitude < meep.dom.speed)
                    {
                        Meep Parent = Random.Range(0, 2) == 0 ? meep : bestM;
                        Meep newMeep = new Meep(Parent.dom, Parent.res, Parent, MeepMesh);
                        newMeep.ob.transform.position = (bestM.ob.transform.position + meep.ob.transform.position) / 2; 
                        meeps.Add(
                            newMeep
                        );

                        meep.Food -= 50;
                        bestM.Food -= 50;
                    }
                    else
                    {
                        dir /= dir.magnitude;
                        meep.ob.transform.position += dir * meep.dom.speed;
                    }


                    i++;
                    continue;
                }
            }
            
            
            Resource best = null;
            float score = float.MaxValue;
            for (int j = 0; j < resources.Count; j++)
            {
                Resource resource = resources[j];
                float distance = (meep.ob.transform.position - resource.ob.transform.position).magnitude;
                if (distance > meep.dom.sight) continue;
                if (meep.dom.maxFood < meep.Food + resource.gene.points)
                {
                    continue;
                }
                float resourceScore = distance;
                if (resourceScore < score)
                {
                    best = resource;
                    score = resourceScore;
                }
            }
            if (best == null)
            {
                Vector2 dir2D = Random.insideUnitCircle.normalized;
                Vector3 dir = new Vector3(dir2D.x, 0, dir2D.y) * meep.dom.speed;
                meep.ob.transform.position += dir;
            }
            else
            {
                Vector3 dir = best.ob.transform.position - meep.ob.transform.position;
                if (dir.magnitude < meep.dom.speed)
                {
                    meep.ob.transform.position = best.ob.transform.position;
                    meep.Food += best.gene.points;

                    Destroy(best.ob);
                    resources.Remove(best);
                }
                else
                {
                    dir /= dir.magnitude;
                    meep.ob.transform.position += dir * meep.dom.speed;
                }
            }


            meep.ob.transform.position = Vector3.ClampMagnitude(meep.ob.transform.position, 30);
            i++;
        }

        for (int i = 0; i < resources.Count;)
        {
            Resource resource = resources[i];
            resource.life -= 1;
            if (resource.life <= 0)
            {
                for (int j = 0; j < 2; j++)
                {
                    if (resources.Count >= maxReasources) continue;
                    resources.Add(
                        new Resource(resource.gene, resource, ResourceMesh)
                    );
                }
                Destroy(resource.ob);
                resources.Remove(resource);
                continue;
            }
            i++;
        }
    }
}

public class Meep
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int Food { get; set; }
    public int Water { get; set; }
    public Gene_Meep dom { get; set; } //FK
    public Gene_Meep res { get; set; } //FK
    public Meep Parent { get; set; } //FK

    public GameObject ob;

    public Meep(Gene_Meep dom_, Gene_Meep res_, Meep parent_, Mesh mesh)
    {
        this.dom = dom_;
        this.res = res_;
        this.Food = 100;
        this.Water = 100;

        this.Parent = parent_;

        this.ob = new GameObject();
        MeshFilter MF = this.ob.AddComponent<MeshFilter>();
        MF.mesh = mesh;
        MeshRenderer MR = this.ob.AddComponent<MeshRenderer>();
        MR.material.color = Color.HSVToRGB(dom_.raceHue, 1, 1);
    }
}

public class Gene_Meep
{
    public float speed;
    public int sight;
    public float raceHue;
    public int maxFood;
    public int maxWater;
    public int minNeededForChild;

    public Gene_Meep()
    {
        this.speed = Random.Range(.1f, 1f);
        this.sight = Random.Range(5, 10);
        this.raceHue = Random.Range(.5f, 1f);
        this.maxFood = Random.Range(80, 120);
        this.maxWater = Random.Range(80, 120);
        this.minNeededForChild = Random.Range(50, Mathf.Min(this.maxFood, this.maxWater));
    }
}

public class Resource
{
    public Gene_Resource gene;
    public GameObject ob;
    public int Parent;
    public int life;

    public Resource(Gene_Resource gene, Resource Parent, Mesh mesh)
    {
        this.gene = gene;
        this.life = gene.life + Random.Range(-5, 5);

        this.ob = new GameObject();
        MeshFilter MF = this.ob.AddComponent<MeshFilter>();
        MF.mesh = mesh;
        MeshRenderer MR = this.ob.AddComponent<MeshRenderer>();
        MR.material.color = Color.HSVToRGB(gene.hue, 1, 1);

        if (Parent == null)
        {
            Vector2 ran = Random.insideUnitCircle * 30f;
            this.ob.transform.position = new Vector3(ran.x, 0, ran.y);
        }
        else
        {
            Vector2 dir2D = Random.insideUnitCircle.normalized;
            Vector3 dir = new Vector3(dir2D.x, 0, dir2D.y);
            this.ob.transform.position = Parent.ob.transform.position + dir;
            this.ob.transform.position = Vector3.ClampMagnitude(this.ob.transform.position, 30);
        }

        this.ob.transform.localScale = Vector3.one * .5f;
    }
}

public class Gene_Resource
{
    public int points;
    public float hue;
    public int life;

    public Gene_Resource()
    {
        this.points = Random.Range(10, 30);
        this.hue = Random.Range(0f, .5f);
        this.life = Random.Range(10, 30);
    }
}