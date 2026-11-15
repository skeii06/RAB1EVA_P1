using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Shared full-screen menu, controls, death, and victory flow for Roll of Fear.
/// The backgrounds are the four panels supplied as the game's UI reference.
/// </summary>
public sealed class HorrorGameFlow : MonoBehaviour
{
    const string MenuScene = "SCN_MainMenu";
    const string FirstLevelScene = "SCN_LVL_CH";
    const string VictoryScene = "SCN_Victory";

    enum ScreenKind { Hidden, Menu, Controls, Death, Victory }

    static HorrorGameFlow instance;

    Canvas screenCanvas;
    RawImage background;
    AudioSource deathAudio;
    GameObject ownedEventSystem;
    ScreenKind currentScreen = ScreenKind.Hidden;
    bool deathShown;

    public static bool TryShowDeath(AudioClip stinger)
    {
        return instance != null && instance.ShowDeath(stinger);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var root = new GameObject("The Roll of Fear | flujo de pantallas");
        root.AddComponent<HorrorGameFlow>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildCanvas();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    void BuildCanvas()
    {
        var canvasRoot = new GameObject("Pantallas | lienzo global", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.transform.SetParent(transform, false);
        screenCanvas = canvasRoot.GetComponent<Canvas>();
        screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        screenCanvas.sortingOrder = 32000;

        var scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var backgroundObject = new GameObject("Arte de pantalla", typeof(RectTransform), typeof(RawImage));
        backgroundObject.transform.SetParent(canvasRoot.transform, false);
        background = backgroundObject.GetComponent<RawImage>();
        StretchToCanvas(background.rectTransform);
        background.raycastTarget = false;

        deathAudio = gameObject.AddComponent<AudioSource>();
        deathAudio.playOnAwake = false;
        deathAudio.loop = false;
        deathAudio.spatialBlend = 0f;
        deathAudio.ignoreListenerPause = true;
        screenCanvas.enabled = false;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;
        deathShown = false;
        EnsureEventSystem(scene);

        if (scene.name == MenuScene)
        {
            DisableSceneCanvases(scene);
            SetScreen(ScreenKind.Menu);
        }
        else if (scene.name == VictoryScene)
        {
            DisableSceneCanvases(scene);
            SetScreen(ScreenKind.Victory);
        }
        else
        {
            SetScreen(ScreenKind.Hidden);
        }
    }

    static void DisableSceneCanvases(Scene scene)
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            if (canvas != null && canvas.gameObject.scene == scene)
                canvas.enabled = false;
        }
    }

    void EnsureEventSystem(Scene scene)
    {
        bool sceneHasEventSystem = false;
        foreach (var eventSystem in FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (eventSystem.gameObject.scene == scene)
            {
                sceneHasEventSystem = true;
                break;
            }
        }

        if (sceneHasEventSystem)
        {
            if (ownedEventSystem != null) Destroy(ownedEventSystem);
            ownedEventSystem = null;
            return;
        }
        if (EventSystem.current != null) return;

        var eventObject = new GameObject("EventSystem | pantallas de terror", typeof(EventSystem), typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(eventObject);
        ownedEventSystem = eventObject;
    }

    bool ShowDeath(AudioClip stinger)
    {
        if (deathShown) return true;
        deathShown = true;
        Time.timeScale = 1f;
        FreezeGameplayActors();
        SetScreen(ScreenKind.Death);
        if (stinger != null && deathAudio != null)
        {
            deathAudio.Stop();
            deathAudio.PlayOneShot(stinger, 0.9f);
        }
        return true;
    }

    static void FreezeGameplayActors()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var player in players)
        {
            player.enabled = false;
            var body = player.GetComponent<Rigidbody>();
            if (body == null) continue;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        var monsters = FindObjectsByType<ForestMonsterChase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var monster in monsters) monster.enabled = false;
    }

    void SetScreen(ScreenKind screen)
    {
        currentScreen = screen;
        ClearHotspots();
        if (screen == ScreenKind.Hidden)
        {
            screenCanvas.enabled = false;
            return;
        }

        string resourceName = screen switch
        {
            ScreenKind.Menu => "Menu",
            ScreenKind.Controls => "Controls",
            ScreenKind.Death => "Death",
            ScreenKind.Victory => "Victory",
            _ => string.Empty
        };
        background.texture = Resources.Load<Texture2D>("HorrorScreens/" + resourceName);
        background.color = Color.white;
        screenCanvas.enabled = true;

        if (background.texture == null)
            Debug.LogError("Falta el fondo de pantalla: Resources/HorrorScreens/" + resourceName + ".png");

        switch (screen)
        {
            case ScreenKind.Menu:
                AddHotspot(252, 228, 449, 284, () => LoadScene(FirstLevelScene));
                AddHotspot(252, 281, 449, 337, () => SetScreen(ScreenKind.Controls));
                AddHotspot(252, 380, 449, 436, QuitGame);
                break;
            case ScreenKind.Controls:
                // The supplied image is displayed at different aspect ratios in editor
                // windows. Use a generous lower-panel target around its visible Back button.
                AddHotspot(285, 345, 570, 469, () => SetScreen(ScreenKind.Menu));
                break;
            case ScreenKind.Death:
                AddHotspot(300, 248, 535, 326, RetryLevel);
                AddHotspot(300, 312, 535, 382, () => LoadScene(MenuScene));
                break;
            case ScreenKind.Victory:
                AddHotspot(300, 330, 590, 438, () => LoadScene(MenuScene));
                break;
        }
    }

    void AddHotspot(float xMin, float yMinFromTop, float xMax, float yMaxFromTop, Action action)
    {
        var buttonObject = new GameObject("Acción de pantalla", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(background.transform, false);
        var rect = buttonObject.GetComponent<RectTransform>();
        float width = background.texture != null ? background.texture.width : 835f;
        float height = background.texture != null ? background.texture.height : 469f;
        rect.anchorMin = new Vector2(xMin / width, 1f - yMaxFromTop / height);
        rect.anchorMax = new Vector2(xMax / width, 1f - yMinFromTop / height);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => action());
    }

    void ClearHotspots()
    {
        if (background == null) return;
        for (int i = background.transform.childCount - 1; i >= 0; i--)
        {
            var hotspot = background.transform.GetChild(i).gameObject;
            // Destroy is deferred until the end of the frame. Disable first so a
            // previous screen's hotspot cannot intercept the next screen's click.
            hotspot.SetActive(false);
            Destroy(hotspot);
        }
    }

    static void StretchToCanvas(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    static void RetryLevel()
    {
        Time.timeScale = 1f;
        string currentLevel = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(currentLevel))
            SceneManager.LoadScene(currentLevel);
    }

    static void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit was selected.");
    }
}
