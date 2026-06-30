using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class Menu : MonoBehaviour
{
    public GameObject viewport;
    public GameObject Prefab;

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
            }
        }
        else
        {
            Directory.CreateDirectory(savesPath);
        }

        GameObject newSave = Instantiate(Prefab);


        
    }
}
