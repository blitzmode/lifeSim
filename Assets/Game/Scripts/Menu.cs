using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class Menu : MonoBehaviour
{
    public GameObject viewport;
    public GameObject Prefab;
    public static string SaveFilePath;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        string savesPath = Path.Combine(Application.persistentDataPath, "saves");

        if (Directory.Exists(savesPath))
        {
            string[] files = Directory.GetFiles(savesPath);

            for (int i = 0; i < files.Length; i++)
            {
                Debug.Log(files[i]);
                GameObject Save = Instantiate(Prefab);
                Save.transform.parent = viewport.transform;
            }
        }
        else
        {
            Directory.CreateDirectory(savesPath);
        }

        GameObject newSave = Instantiate(Prefab);
        newSave.transform.parent = viewport.transform;
        newSave.GetComponent<Button>().onClick.AddListener(delegate
        {
            SaveFilePath = Path.Combine(savesPath, "save1");

            this.gameObject.AddComponent<Manager>();
        });

        
    }
}
