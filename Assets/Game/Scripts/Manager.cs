
using System.Collections.Generic;
using UnityEngine;
using SQLite4Unity3d;

public class Manager : MonoBehaviour
{
    public Mesh MeepMesh;
    public Mesh ResourceMesh;

    public static List<Meep> meeps = new List<Meep>();
    public static List<Gene_Meep> meepGenes = new List<Gene_Meep>();
    public static List<Resource> resources = new List<Resource>();
    public static List<Gene_Resource> resourceGene = new List<Gene_Resource>();

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
        db.CreateTable<Gene_Meep>();
        db.CreateTable<Resource>();
        db.CreateTable<Gene_Resource>();

        for (int i = 0; i < startingMeeps; i++)
        {
            Gene_Meep dom = new Gene_Meep();
            Gene_Meep res = new Gene_Meep();
            Meep newMeep = new Meep(dom.Id, res.Id, 0, MeepMesh);
            Vector2 ran = Random.insideUnitCircle * 30f;
            newMeep.ob.transform.position = new Vector3(ran.x, 0, ran.y);
        }
        db.InsertAll(meeps);
        db.InsertAll(meepGenes);

        for (int i = 0; i < startingReasources; i++)
        {
            Gene_Resource gene = new Gene_Resource();
            new Resource(gene.Id, 0, ResourceMesh);
        }
        db.InsertAll(resources);
        db.InsertAll(resourceGene);
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
    [PrimaryKey]
    public int Id { get; set; }
    public int Food { get; set; }
    public int Water { get; set; }
    public int Dom { get; set; } //FK
    public int Res { get; set; } //FK
    public int Parent { get; set; } //FK

    public GameObject ob;

    public Meep(int dom_, int res_, int parent_, Mesh mesh)
    {
        this.Id = Manager.meeps.Count;
        this.Dom = dom_;
        this.Dom = res_;
        this.Food = 100;
        this.Water = 100;

        this.Parent = parent_;

        this.ob = new GameObject();
        MeshFilter MF = this.ob.AddComponent<MeshFilter>();
        MF.mesh = mesh;
        MeshRenderer MR = this.ob.AddComponent<MeshRenderer>();
        MR.material.color = Color.HSVToRGB(Manager.meepGenes[dom_].Hue, 1, 1);

        Manager.meeps.Add(this);
    }
}

public class Gene_Meep
{
    [PrimaryKey]
    public int Id { get; set; }
    public float Speed { get; set; }
    public int Sight { get; set; }
    public float Hue { get; set; }
    public int MaxFood { get; set; }
    public int MaxWater { get; set; }
    public int MinNeededForChild { get; set; }

    public Gene_Meep()
    {
        this.Id = Manager.meepGenes.Count;
        this.Speed = Random.Range(.1f, 1f);
        this.Sight = Random.Range(5, 10);
        this.Hue = Random.Range(.5f, 1f);
        this.MaxFood = Random.Range(80, 120);
        this.MaxWater = Random.Range(80, 120);
        this.MinNeededForChild = Random.Range(50, Mathf.Min(this.MaxFood, this.MaxWater));

        Manager.meepGenes.Add(this);
    }
}

public class Resource
{
    [PrimaryKey]
    public int Id { get; set; }
    public int Gene { get; set; }
    public int Parent { get; set; }
    public int Life { get; set; }

    public GameObject ob;

    public Resource(int gene, int Parent, Mesh mesh)
    {
        this.Id = Manager.resources.Count;
        this.Gene = gene;
        this.Life = Manager.resourceGene[gene].Life + Random.Range(-5, 5);
        this.ob = new GameObject();

        MeshFilter MF = this.ob.AddComponent<MeshFilter>();
        MF.mesh = mesh;
        MeshRenderer MR = this.ob.AddComponent<MeshRenderer>();
        MR.material.color = Color.HSVToRGB(Manager.resourceGene[gene].Hue, 1, 1);

        if (Parent == 0)
        {
            Vector2 ran = Random.insideUnitCircle * 30f;
            this.ob.transform.position = new Vector3(ran.x, 0, ran.y);
        }
        else
        {
            Vector2 dir2D = Random.insideUnitCircle.normalized;
            Vector3 dir = new Vector3(dir2D.x, 0, dir2D.y);
            this.ob.transform.position = Manager.resources[Parent].ob.transform.position + dir;
            this.ob.transform.position = Vector3.ClampMagnitude(this.ob.transform.position, 30);
        }

        this.ob.transform.localScale = Vector3.one * .5f;

        Manager.resources.Add(this);
    }
}

public class Gene_Resource
{
    public int Id { get; set; }
    public int Points { get; set; }
    public float Hue { get; set; }
    public int Life { get; set; }

    public Gene_Resource()
    {
        this.Id = Manager.resourceGene.Count;
        this.Points = Random.Range(10, 30);
        this.Hue = Random.Range(0f, .5f);
        this.Life = Random.Range(10, 30);

        Manager.resourceGene.Add(this);
    }
}