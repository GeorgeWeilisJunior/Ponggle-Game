using UnityEngine;

public class AppBootstrap : MonoBehaviour
{
    [SerializeField] private string firstScene = "MainMenu";
    [SerializeField] private bool loadOnStart = true;

    void Start()
    {
        if (loadOnStart && !string.IsNullOrEmpty(firstScene))
            SceneTransition.LoadScene(firstScene);
    }
}
