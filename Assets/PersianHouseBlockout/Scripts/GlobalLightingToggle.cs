using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[DisallowMultipleComponent]
public sealed class GlobalLightingToggle : MonoBehaviour
{
    public bool IsOn { get; private set; }=true;
    Light[] sceneLights;
    bool[] initialStates;
    float ambient,reflections;
    Volume darkness;
    VolumeProfile profile;
    ChildFirstPersonController player;
    bool initialized;
    GUIStyle style,hint;
    public static Rect ButtonRect => new Rect(Mathf.Max(12,Screen.width-248),16,232,40);
    public static bool IsPointerOverButton(Vector2 screenPosition)
    {return ButtonRect.Contains(new Vector2(screenPosition.x,Screen.height-screenPosition.y));}
    void Awake(){Initialize();}
    void Initialize()
    {
        if(initialized)return;
        player=GetComponent<ChildFirstPersonController>();
        sceneLights=FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(l=>l.gameObject.scene==gameObject.scene && !l.transform.IsChildOf(transform)).ToArray();
        initialStates=sceneLights.Select(l=>l.enabled).ToArray();
        ambient=RenderSettings.ambientIntensity;reflections=RenderSettings.reflectionIntensity;
        var obj=new GameObject("Runtime global-lighting override");obj.transform.SetParent(transform,false);
        darkness=obj.AddComponent<Volume>();darkness.isGlobal=true;darkness.priority=10000;darkness.weight=0;
        profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Runtime darkness";
        var indirect=profile.Add<IndirectLightingController>(true);
        indirect.indirectDiffuseLightingMultiplier.Override(0);
        indirect.reflectionLightingMultiplier.Override(0);
        indirect.reflectionProbeIntensityMultiplier.Override(0);
        darkness.sharedProfile=profile;initialized=true;
    }
    void Update()
    {
        if(Application.isFocused && player!=null && player.InputEnabled && Keyboard.current!=null && Keyboard.current.lKey.wasPressedThisFrame)
            SetLighting(!IsOn);
    }
    public void SetLighting(bool on)
    {
        Initialize();
        for(int i=0;i<sceneLights.Length;i++)if(sceneLights[i]!=null)sceneLights[i].enabled=on && initialStates[i];
        RenderSettings.ambientIntensity=on?ambient:0;
        RenderSettings.reflectionIntensity=on?reflections:0;
        darkness.weight=on?0:1;IsOn=on;
    }
    void OnGUI()
    {
        if(player==null || !player.InputEnabled)return;
        if(style==null){style=new GUIStyle(GUI.skin.button){fontSize=15,fontStyle=FontStyle.Bold};hint=new GUIStyle(GUI.skin.label){fontSize=12,alignment=TextAnchor.UpperRight};}
        bool enabled=GUI.enabled;GUI.enabled=Cursor.lockState!=CursorLockMode.Locked;
        if(GUI.Button(ButtonRect,"Global Lighting: "+(IsOn?"ON":"OFF")+"  [L]",style))SetLighting(!IsOn);
        GUI.enabled=enabled;
        GUI.Label(new Rect(ButtonRect.x,59,ButtonRect.width,22),"L to toggle • Esc to click",hint);
    }
    void OnDisable(){if(initialized)SetLighting(true);}
    void OnDestroy(){if(profile!=null)Destroy(profile);}
}
