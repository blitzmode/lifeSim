using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using SQLite4Unity3d;
using TMPro;
using System.Linq;

public class Manager : MonoBehaviour
{
    // in menu
    bool inMenu;
    string SaveFilePath;
    public GameObject viewport;
    public GameObject Prefab;

    // in game
    public Mesh MeepMesh;
    public static Mesh SMeepMesh;
    public Mesh ResourceMesh;
    public static Mesh SResourceMesh;

    public static List<Meep> meeps = new List<Meep>();
    public static List<Gene_Meep> meepGenes = new List<Gene_Meep>();
    public static List<Resource> resources = new List<Resource>();
    public static List<Gene_Resource> resourceGene = new List<Gene_Resource>();

    public static int meepCount;
    public static int resourceCount;

    int maxReasources = 100;
    int startingMeeps = 100;
    int startingReasources = 100;

    SQLiteConnection db;

    void Start()
    {
        SMeepMesh = MeepMesh;
        SResourceMesh = ResourceMesh;
        MenuStart();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void MenuStart()
    {
        // clear game

        // start menu
        inMenu = true;
        viewport.transform.parent.gameObject.SetActive(true);

        GameObject addButton(string name)
        {
            GameObject button = Instantiate(Prefab);
            button.transform.SetParent(viewport.transform);
            button.transform.GetChild(0).GetComponent<TMP_Text>().text = name;
            return button;
        }

        string savesPath = Path.Combine(Application.persistentDataPath, "saves");
        if (Directory.Exists(savesPath))
        {
            string[] files = Directory.GetFiles(savesPath);
            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                addButton(files[i]).GetComponent<Button>().onClick.AddListener(delegate
                {
                    SaveFilePath = filePath;
                    GameStart();
                });
            }
        }
        else
        {
            Directory.CreateDirectory(savesPath);
        }

        addButton("new save").GetComponent<Button>().onClick.AddListener(delegate
        {
            int saveNumber = 1;
            do
            {
                SaveFilePath = Path.Combine(savesPath, "save" + saveNumber);
                saveNumber++;
            }
            while (File.Exists(SaveFilePath));
            GameStart();
        });

        addButton("open folder").GetComponent<Button>().onClick.AddListener(delegate
        {
            Application.OpenURL("file://" + savesPath);
        });
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void GameStart()
    {
        //clear menu
        for (int i = viewport.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(viewport.transform.GetChild(i).gameObject);
        }
        viewport.transform.parent.gameObject.SetActive(false);

        // start game
        inMenu = false;
        if (File.Exists(SaveFilePath))
        {
            db = new SQLiteConnection(SaveFilePath, SQLiteOpenFlags.ReadWrite);
            meepGenes = db.Table<Gene_Meep>().ToList<Gene_Meep>();
            resourceGene = db.Table<Gene_Resource>().ToList<Gene_Resource>();
            meeps = db.Table<Meep>().Where(m => m.Food > 0 && m.Water > 0).ToList<Meep>();
            meepCount = db.Table<Meep>().Count();
            resources = db.Table<Resource>().Where(m => m.Life > 0).ToList<Resource>();
            resourceCount = db.Table<Resource>().Count();
        }
        else
        {
            db = new SQLiteConnection(SaveFilePath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite);
            db.CreateTable<Meep>();
            db.CreateTable<Gene_Meep>();
            db.CreateTable<Resource>();
            db.CreateTable<Gene_Resource>();

            meepCount = 0;
            resourceCount = 0;

            for (int i = 0; i < startingMeeps; i++)
            {
                Gene_Meep dom = new Gene_Meep(Random.Range(.1f, 1f), Random.Range(5f, 10f), Random.Range(80, 120), Random.Range(80, 120));
                Gene_Meep res = new Gene_Meep(Random.Range(.1f, 1f), Random.Range(5f, 10f), Random.Range(80, 120), Random.Range(80, 120));
                Meep newMeep = new Meep(dom.Id, res.Id, -1);
                Vector2 ran = Random.insideUnitCircle * 30f;
                newMeep.ob.transform.position = new Vector3(ran.x, 0, ran.y);
            }
            db.InsertAll(meeps);
            db.InsertAll(meepGenes);

            for (int i = 0; i < startingReasources; i++)
            {
                Gene_Resource gene = new Gene_Resource(Random.Range(5, 20));
                Resource newResource = new Resource(gene.Id, -1);
                Vector2 ran = Random.insideUnitCircle * 30f;
                newResource.ob.transform.position = new Vector3(ran.x, 0, ran.y);
            }
            db.InsertAll(resources);
            db.InsertAll(resourceGene);
        }
    }
    // Update is called once per frame
    void Update()
    {
        if (inMenu) return;
        for (int i = 0; i < meeps.Count;)
        {
            Meep meep = meeps[i];

            meep.Water -= 0;
            meep.Food -= 1;

            db.Update(meep);
            
            if (meep.Water <= 0)
            {
                Destroy(meep.ob);
                meeps.Remove(meep);
                continue;
            }

            if (meep.Food <= 0)
            {
                Destroy(meep.ob);
                meeps.Remove(meep);
                continue;
            }

            if (meep.Food >= Manager.meepGenes[meep.Dom].MinNeededForChild)
            {
                Meep bestM = null;
                float scoreM = float.MaxValue;
                for (int k = 0; k < meeps.Count; k++)
                {
                    if (i == k) continue;
                    Meep OtherMeep = meeps[k];
                    if (meep.Food >= Manager.meepGenes[meep.Dom].MinNeededForChild && OtherMeep.Food > Manager.meepGenes[OtherMeep.Dom].MinNeededForChild)
                    {
                        float distance = (meep.ob.transform.position - OtherMeep.ob.transform.position).magnitude;
                        if (distance > Manager.meepGenes[meep.Dom].Sight) continue;
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
                    if (dir.magnitude < Manager.meepGenes[meep.Dom].Speed)
                    {
                        Meep Parent = Random.Range(0, 2) == 0 ? meep : bestM;
                        Meep newMeep = new Meep(Parent.Dom, Parent.Res, Parent.Id);
                        newMeep.ob.transform.position = (bestM.ob.transform.position + meep.ob.transform.position) / 2; 

                        meep.Food -= 50;
                        bestM.Food -= 50;
                    }
                    else
                    {
                        dir /= dir.magnitude;
                        meep.ob.transform.position += dir * Manager.meepGenes[meep.Dom].Speed;
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
                if (distance > Manager.meepGenes[meep.Dom].Sight) continue;
                if (Manager.meepGenes[meep.Dom].MaxFood < meep.Food + Manager.resourceGene[resource.Gene].Points)
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
                Vector3 dir = new Vector3(dir2D.x, 0, dir2D.y) * Manager.meepGenes[meep.Dom].Speed;
                meep.ob.transform.position += dir;
            }
            else
            {
                Vector3 dir = best.ob.transform.position - meep.ob.transform.position;
                if (dir.magnitude < Manager.meepGenes[meep.Dom].Speed)
                {
                    meep.ob.transform.position = best.ob.transform.position;
                    meep.Food += Manager.resourceGene[best.Gene].Points;

                    Destroy(best.ob);
                    resources.Remove(best);
                }
                else
                {
                    dir /= dir.magnitude;
                    meep.ob.transform.position += dir * Manager.meepGenes[meep.Dom].Speed;
                }
            }


            meep.ob.transform.position = Vector3.ClampMagnitude(meep.ob.transform.position, 30);
            i++;
        }

        for (int i = 0; i < resources.Count;)
        {
            Resource resource = resources[i];
            resource.Life -= 1;
            if (resource.Life <= 0)
            {
                for (int j = 0; j < 2; j++)
                {
                    if (resources.Count >= maxReasources) continue;
                    Resource newResource = new Resource(resource.Gene, resource.Id);
                    Vector2 dir2D = Random.insideUnitCircle.normalized;
                    Vector3 dir = new Vector3(dir2D.x, 0, dir2D.y);
                    newResource.ob.transform.position = Vector3.ClampMagnitude(resource.ob.transform.position + dir, 30);
                }
            }
            db.Update(resource);
            if (resource.Life <= 0)
            {
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
    public float PosX { get; set; }
    public float PosY { get; set; }

    public GameObject ob;

    public Meep()
    {
        this.ob = new GameObject("Meep");
        this.ob.transform.position = new Vector3(this.PosX, 0, this.PosY);
        MeshFilter MF = this.ob.AddComponent<MeshFilter>();
        MF.mesh = Manager.SMeepMesh;
        MeshRenderer MR = this.ob.AddComponent<MeshRenderer>();
        MR.material.color = Color.HSVToRGB(Manager.meepGenes[this.Dom].Hue, 1, 1);
    }
    public Meep(int dom_, int res_, int parent_)
    {
        this.Id = Manager.meepCount;
        Manager.meepCount++;

        this.Dom = dom_;
        this.Res = res_;
        this.Food = Manager.meepGenes[dom_].MaxFood;
        this.Water = Manager.meepGenes[dom_].MaxWater;
        this.Parent = parent_;
        this.ob = new GameObject("Meep");
        MeshFilter MF = this.ob.AddComponent<MeshFilter>();
        MF.mesh = Manager.SMeepMesh;
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
    public float Sight { get; set; }
    public float Hue { get; set; }
    public int MaxFood { get; set; }
    public int MaxWater { get; set; }
    public int MinNeededForChild { get; set; }

    public Gene_Meep(){}
    public Gene_Meep(float Speed, float sight, int maxFood, int maxWater)
    {
        this.Id = Manager.meepGenes.Count;
        this.Speed = Speed;
        this.Sight = sight;
        this.Hue = Random.Range(.5f, 1f);
        this.MaxFood = maxFood;
        this.MaxWater = maxWater;
        this.MinNeededForChild = Mathf.Min(maxFood, maxWater) - 10;

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
    public float PosX { get; set; }
    public float PosY { get; set; }

    public GameObject ob;

    public Resource()
    {
        this.ob = new GameObject("Resource");
        this.ob.transform.position = new Vector3(this.PosX, 0, this.PosY);
        MeshFilter MF = this.ob.AddComponent<MeshFilter>();
        MF.mesh = Manager.SResourceMesh;
        MeshRenderer MR = this.ob.AddComponent<MeshRenderer>();
        MR.material.color = Color.HSVToRGB(Manager.resourceGene[this.Gene].Hue, 1, 1);
    }
    public Resource(int gene, int Parent)
    {
        this.Id = Manager.resourceCount;
        Manager.resourceCount += 1;

        this.Gene = gene;
        this.Life = Manager.resourceGene[gene].Points + Random.Range(-5, 5);
        this.ob = new GameObject("Resource");

        MeshFilter MF = this.ob.AddComponent<MeshFilter>();
        MF.mesh = Manager.SResourceMesh;
        MeshRenderer MR = this.ob.AddComponent<MeshRenderer>();
        MR.material.color = Color.HSVToRGB(Manager.resourceGene[gene].Hue, 1, 1);

        this.ob.transform.localScale = Vector3.one * .5f;

        Manager.resources.Add(this);
    }
}

public class Gene_Resource
{
    public int Id { get; set; }
    public int Points { get; set; }
    public float Hue { get; set; }

    public Gene_Resource(){}
    public Gene_Resource(int points)
    {
        this.Id = Manager.resourceGene.Count;
        this.Points = points;
        this.Hue = Random.Range(0f, .5f);

        Manager.resourceGene.Add(this);
    }
}