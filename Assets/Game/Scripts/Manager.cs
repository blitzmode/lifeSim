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
    public GameObject content;
    public GameObject Prefab;

    public GameObject closeButton;
    public GameObject killAllButton;

    // in game
    public Mesh MeepMesh;
    public static Mesh SMeepMesh;
    public Mesh ResourceMesh;
    public static Mesh SResourceMesh;

    public static List<Meep> meeps = new List<Meep>();
    public static List<Gene_Meep> meepGenes = new List<Gene_Meep>();
    public static List<Resource> resources = new List<Resource>();
    public static List<Gene_Resource> resourceGene = new List<Gene_Resource>();
    public static DataBaceInfo dataBaceInfo;

    public static int meepCount;
    public static int resourceCount;

    int maxReasources = 100;
    int startingGenes = 10;
    int startingMeepsPerGene = 3;
    int startingReasources = 100;

    public static int ResourcesLifeSpan = 100;
    public static int FoodNeddedForChild = 50;

    // Meep Gene Stats
    float MGMinSpeed = .1f;
    float MGMaxSpeed = .5f;
    float MGMinSight = 5f;
    float MGMaxSight = 10f;
    int MGMaxFull = 100;

    // grapth
    int mode = 0;
    public GameObject grapth;

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
        // clear
        grapth.SetActive(false);

        // start menu
        inMenu = true;
        List<GameObject> buttons = new List<GameObject>();

        GameObject addButton(string name)
        {
            GameObject button = Instantiate(Prefab);
            button.transform.SetParent(content.transform);
            button.transform.GetChild(0).GetComponent<TMP_Text>().text = name;
            buttons.Add(button);
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

        RectTransform rect = content.GetComponent<RectTransform>();
        rect.anchorMax = new Vector2(1, 1);
        rect.anchorMin = new Vector2(0, 1f - .07f * buttons.Count);

        for (int i = 0; i < buttons.Count; i++)
        {
            RectTransform buttonRect = buttons[i].GetComponent<RectTransform>();
            buttonRect.anchorMax = new Vector2(1, (float)(i + 1) / (float)buttons.Count);
            buttonRect.anchorMin = new Vector2(0, (float)i / (float)buttons.Count);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void GameStart()
    {
        //clear
        for (int i = content.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(content.transform.GetChild(i).gameObject);
        }
        content.transform.parent.parent.gameObject.SetActive(false);
        grapth.SetActive(false);

        // start game
        inMenu = false;
        if (File.Exists(SaveFilePath))
        {
            db = new SQLiteConnection(SaveFilePath, SQLiteOpenFlags.ReadWrite);
            dataBaceInfo = db.Table<DataBaceInfo>().First();
            meepGenes = db.Table<Gene_Meep>().ToList<Gene_Meep>();
            resourceGene = db.Table<Gene_Resource>().ToList<Gene_Resource>();
            meeps = db.Query<Meep>(
                @"SELECT *
                    FROM (
                        SELECT 
                            m.*,
                            m.Born + ? + COALESCE(SUM(g.Points), 0) - ? -
                            (SELECT COUNT(*) FROM Meep child WHERE child.Parent1 = m.Id OR child.Parent2 = m.Id ) * ?
                            AS Food
                        FROM Meep m
                        LEFT JOIN Resource r ON r.EantenBy = m.Id
                        LEFT JOIN Gene_Resource g ON g.Id = r.Gene
                        GROUP BY m.Id
                    )
                    WHERE Food > 0",
                FoodNeddedForChild * 2,
                dataBaceInfo.Tick,
                FoodNeddedForChild
            );
            for (int i = 0; i < meeps.Count; i++)
            {
                meeps[i].ob = new GameObject("Meep");
                meeps[i].ob.transform.position = new Vector3(meeps[i].PosX, 0, meeps[i].PosY);
                MeshFilter MF = meeps[i].ob.AddComponent<MeshFilter>();
                MF.mesh = Manager.SMeepMesh;
                MeshRenderer MR = meeps[i].ob.AddComponent<MeshRenderer>();
                MR.material.color = Color.HSVToRGB(Manager.meepGenes[meeps[i].Gene].Hue, 1, 1);
            }
            meepCount = db.Table<Meep>().Count();
            resources = db.Query<Resource>("SELECT * FROM Resource WHERE Born + ? >= ? AND EantenBy = -1", ResourcesLifeSpan, dataBaceInfo.Tick);
            for (int i = 0; i < resources.Count; i++)
            {
                resources[i].ob = new GameObject("Resource");
                resources[i].ob.transform.position = new Vector3(resources[i].PosX, 0, resources[i].PosY);
                MeshFilter MF = resources[i].ob.AddComponent<MeshFilter>();
                MF.mesh = Manager.SResourceMesh;
                MeshRenderer MR = resources[i].ob.AddComponent<MeshRenderer>();
                MR.material.color = Color.HSVToRGB(Manager.resourceGene[resources[i].Gene].Hue, 1, 1);
            }
            resourceCount = db.Table<Resource>().Count();
        }
        else
        {
            db = new SQLiteConnection(SaveFilePath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite);

            db.CreateTable<DataBaceInfo>();
            db.CreateTable<Meep>();
            db.CreateTable<Gene_Meep>();
            db.CreateTable<Resource>();
            db.CreateTable<Gene_Resource>();
            
            dataBaceInfo = new DataBaceInfo();
            db.Insert(dataBaceInfo);

            meepCount = 0;
            resourceCount = 0;

            for (int i = 0; i < startingGenes; i++)
            {
                Gene_Meep gene = new Gene_Meep(Random.Range(MGMinSpeed, MGMaxSpeed), Random.Range(MGMinSight, MGMaxSight), Random.Range(FoodNeddedForChild, MGMaxFull));
                for (int j = 0; j < startingMeepsPerGene; j++)
                {
                    Meep newMeep = new Meep(gene.Id, -1, -1, Random.insideUnitCircle * 30f);
                }
            }
            db.InsertAll(meeps);
            db.InsertAll(meepGenes);

            for (int i = 0; i < startingReasources; i++)
            {
                Gene_Resource gene = new Gene_Resource(Random.Range(5, 20));
                Resource newResource = new Resource(gene.Id, -1, Random.insideUnitCircle * 30f);
            }
            db.InsertAll(resources);
            db.InsertAll(resourceGene);
        }
    }

    void StatsStart()
    {
        // clear

        // start grapth
        grapth.SetActive(true);
        List<Meep> GetMeepsAtTick(int queryTick)
        {
            return db.Query<Meep>(
                @"SELECT *
                FROM (
                    SELECT 
                        m.Id, m.Born, m.Gene, m.Parent1, m.Parent2,
                        m.PosX, m.PosY, m.Hungery,
                        m.Born + ?1 + COALESCE(SUM(CASE WHEN r.EantenTick <= ?2 THEN g.Points ELSE 0 END), 0) - ?2 -
                        (SELECT COUNT(*) FROM Meep child 
                            WHERE (child.Parent1 = m.Id OR child.Parent2 = m.Id) AND child.Born <= ?2) * ?3
                        AS Food
                    FROM Meep m
                    LEFT JOIN Resource r ON r.EantenBy = m.Id
                    LEFT JOIN Gene_Resource g ON g.Id = r.Gene
                    WHERE m.Born <= ?2
                    GROUP BY m.Id
                )
                WHERE Food > 0",
                FoodNeddedForChild * 2,
                queryTick,
                FoodNeddedForChild
            ).ToList();
        }

        void LoadGrapth()
        {
            LineGraph lineGraph = grapth.GetComponent<LineGraph>();
            int xSize = 100;

            if (mode == 0)
            {
                LineGraph.LineSeries[] series = new LineGraph.LineSeries[meepGenes.Count];
                for (int j = 0; j < meepGenes.Count; j++)
                {
                    series[j] = new LineGraph.LineSeries();
                    series[j].values = new List<float>();
                    series[j].color = Color.HSVToRGB(Manager.resourceGene[j].Hue, 1, 1);
                }

                for (int i = 0; i < xSize; i++)
                {
                    meeps = GetMeepsAtTick(Mathf.FloorToInt((float)(i / (float)xSize) * (float)dataBaceInfo.Tick));
                    int[] GeneCount = new int[meepGenes.Count];
                    foreach (Meep meep in meeps)
                    {
                        GeneCount[meep.Gene] += 1;
                    }
                    for (int j = 0; j < meepGenes.Count; j++)
                    {
                        series[j].values.Add(GeneCount[j]);
                    }
                }
                lineGraph.SetSeries(series.ToList());
            }
        }

        LoadGrapth();
    }
    // Update is called once per frame
    void Update()
    {
        if (inMenu) return;

        if (meeps.Count == 0)
        {
            inMenu = true;
            StatsStart();
            return;
        }

        dataBaceInfo.Tick += 1;
        db.Update(dataBaceInfo);

        for (int i = 0; i < meeps.Count; i++)
        {
            Meep meep = meeps[i];
            if (meep.Born == dataBaceInfo.Tick)
                continue;

            meep.Food -= 1;
            if (meep.Food <= 0)
            {
                db.Update(meep);
                Destroy(meep.ob);
                meeps[i] = meeps[meeps.Count - 1];
                meeps.RemoveAt(meeps.Count - 1);
                i--;
                continue;
            }

            if (meep.Food < FoodNeddedForChild)
                meep.Hungery = true;
            if (meep.Food > meepGenes[meep.Gene].Full)
                meep.Hungery = false;

            Vector2 posV2 = new Vector2(meep.PosX, meep.PosY);
            if (meep.Hungery)
            {
                Resource best = null;
                float score = float.MaxValue;
                for (int j = 0; j < resources.Count; j++)
                {
                    Resource resource = resources[j];
                    float distance = (meep.ob.transform.position - resource.ob.transform.position).magnitude;
                    if (distance > meepGenes[meep.Gene].Sight) continue;
                    float resourceScore = distance;
                    if (resourceScore < score)
                    {
                        best = resource;
                        score = resourceScore;
                    }
                }
                if (best == null)
                {
                    if (meep.target == null || Vector3.Distance(meep.target, posV2) <  meepGenes[meep.Gene].Speed)
                        meep.target = Random.insideUnitCircle * 30;
                    Vector2 dir2D = (meep.target - posV2).normalized;
                    Vector3 dir = new Vector3(dir2D.x, 0, dir2D.y) * meepGenes[meep.Gene].Speed;
                    meep.ob.transform.position += dir;
                }
                else
                {
                    Vector3 dir = best.ob.transform.position - meep.ob.transform.position;
                    if (dir.magnitude < meepGenes[meep.Gene].Speed)
                    {
                        meep.ob.transform.position = best.ob.transform.position;
                        meep.Food += resourceGene[best.Gene].Points;

                        best.EantenBy = meep.Id;
                        best.EantenTick = dataBaceInfo.Tick;
                        db.Update(best);

                        Destroy(best.ob);
                        resources.Remove(best);
                    }
                    else
                    {
                        dir /= dir.magnitude;
                        meep.ob.transform.position += dir * meepGenes[meep.Gene].Speed;
                    }
                }
            }
            else
            {
                Meep bestM = null;
                float scoreM = float.MaxValue;
                for (int k = 0; k < meeps.Count; k++)
                {
                    if (i == k) continue;
                    Meep OtherMeep = meeps[k];
                    if (OtherMeep.Hungery) continue;
                    float distance = (meep.ob.transform.position - OtherMeep.ob.transform.position).magnitude;
                    if (distance > meepGenes[meep.Gene].Sight) continue;
                    if (distance < scoreM)
                    {
                        bestM = OtherMeep;
                        scoreM = distance;
                    }
                }
                if (bestM == null)
                {
                    if (meep.target == null || Vector3.Distance(meep.target, posV2) <  meepGenes[meep.Gene].Speed)
                        meep.target = Random.insideUnitCircle * 30;
                    Vector2 dir2D = (meep.target - posV2).normalized;
                    Vector3 dir = new Vector3(dir2D.x, 0, dir2D.y) * meepGenes[meep.Gene].Speed;
                    meep.ob.transform.position += dir;
                }
                else
                {
                    Vector3 dir = bestM.ob.transform.position - meep.ob.transform.position;
                    if (dir.magnitude < meepGenes[meep.Gene].Speed)
                    {
                        Meep Parent = Random.Range(0, 2) == 0 ? meep : bestM;
                        Vector3 pos = (bestM.ob.transform.position + meep.ob.transform.position) / 2;
                        Meep newMeep = new Meep(Parent.Gene, meep.Id, bestM.Id, new Vector2(pos.x, pos.z));
                        db.Insert(newMeep);

                        meep.Food -= FoodNeddedForChild;
                        bestM.Food -= FoodNeddedForChild;
                    }
                    else
                    {
                        dir /= dir.magnitude;
                        meep.ob.transform.position += dir * meepGenes[meep.Gene].Speed;
                    }
                }
            }
            meep.ob.transform.position = Vector3.ClampMagnitude(meep.ob.transform.position, 30);
            meep.PosX = meep.ob.transform.position.x;
            meep.PosY = meep.ob.transform.position.z;
        }
        db.UpdateAll(meeps);

        for (int i = 0; i < resources.Count;)
        {
            Resource resource = resources[i];
            if (resource.Born + ResourcesLifeSpan < dataBaceInfo.Tick)
            {
                for (int j = 0; j < 2; j++)
                {
                    if (resources.Count >= maxReasources) continue;
                    Vector3 pos = resource.ob.transform.position;
                    Resource newResource = new Resource(resource.Gene, resource.Id, Random.insideUnitCircle.normalized + new Vector2(pos.x, pos.z));
                    db.Insert(newResource);
                }
                Destroy(resource.ob);
                resources.Remove(resource);
                continue;
            }
            i++;
        }
    }
}

public class DataBaceInfo
{
    [PrimaryKey] [AutoIncrement]
    public int Id { get; set; }
    public int Tick { get; set; }
    public DataBaceInfo(){}
} 

public class Meep
{
    [PrimaryKey]
    public int Id { get; set; }
    public int Born { get; set; }
    public int Gene { get; set; } //FK
    public int Parent1 { get; set; } //FK
    public int Parent2 { get; set; } //FK
    public float PosX { get; set; }
    public float PosY { get; set; }
    public bool Hungery { get; set; }
    public int Food { get; set; }

    public GameObject ob;
    public Vector2 target;

    public Meep() {}
    public Meep(int gene_, int parent1_, int parent2_, Vector2 pos)
    {
        this.Id = Manager.meepCount;
        Manager.meepCount++;
        this.Born = Manager.dataBaceInfo.Tick;
        this.Gene = gene_;
        this.Food = Manager.FoodNeddedForChild * 2;
        this.Parent1 = parent1_;
        this.Parent2 = parent2_;
        this.Hungery = true;

        this.ob = new GameObject("Meep");
        MeshFilter MF = this.ob.AddComponent<MeshFilter>();
        MF.mesh = Manager.SMeepMesh;
        MeshRenderer MR = this.ob.AddComponent<MeshRenderer>();
        MR.material.color = Color.HSVToRGB(Manager.meepGenes[gene_].Hue, 1, 1);
        this.ob.transform.position = new Vector3(pos.x, 0, pos.y);
        this.PosX = pos.x;
        this.PosY = pos.y;

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
    public int Full { get; set; }

    public Gene_Meep(){}
    public Gene_Meep(float Speed, float sight, int Full)
    {
        this.Id = Manager.meepGenes.Count;
        this.Speed = Speed;
        this.Sight = sight;
        this.Hue = Random.Range(.5f, 1f);
        this.Full = Full;

        Manager.meepGenes.Add(this);
    }
}

public class Resource
{
    [PrimaryKey]
    public int Id { get; set; }
    public int Gene { get; set; }
    public int Parent { get; set; }
    public int Born { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public int EantenBy { get; set; }
    public int EantenTick { get; set; }

    public GameObject ob;

    public Resource(){}
    public Resource(int gene, int Parent, Vector2 pos)
    {
        this.Id = Manager.resourceCount;
        Manager.resourceCount += 1;

        this.Parent = Parent;
        this.Gene = gene;
        this.Born = Manager.dataBaceInfo.Tick - Random.Range(0, 10);
        this.EantenBy = -1;

        pos = Vector2.ClampMagnitude(pos, 30);

        this.PosX = pos.x;
        this.PosY = pos.y;

        this.ob = new GameObject("Resource");
        MeshFilter MF = this.ob.AddComponent<MeshFilter>();
        MF.mesh = Manager.SResourceMesh;
        MeshRenderer MR = this.ob.AddComponent<MeshRenderer>();
        MR.material.color = Color.HSVToRGB(Manager.resourceGene[gene].Hue, 1, 1);

        this.ob.transform.position = new Vector3(pos.x, 0, pos.y);
        this.ob.transform.localScale = Vector3.one * .5f;

        Manager.resources.Add(this);
    }
}

public class Gene_Resource
{
    [PrimaryKey]
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