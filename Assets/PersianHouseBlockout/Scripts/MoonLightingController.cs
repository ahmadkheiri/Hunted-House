using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways, DisallowMultipleComponent]
public sealed class MoonLightingController : MonoBehaviour
{
    public static MoonLightingController Current {get;private set;}
    public Light moonLight;
    [Range(5,85)] public float elevation=45;
    [Range(0,360)] public float azimuth=0;
    [Range(0,1)] public float intensityLux=.15f;
    void OnEnable(){Current=this;Apply();}
    void OnDisable(){if(Current==this)Current=null;}
    void OnValidate(){Apply();}
    public void Apply()
    {
        elevation=Mathf.Clamp(elevation,5,85);azimuth=Mathf.Repeat(azimuth,360);intensityLux=Mathf.Clamp(intensityLux,0,1);
        if(moonLight==null)return;
        moonLight.transform.rotation=Quaternion.Euler(elevation,azimuth+180,0);
        moonLight.type=LightType.Directional;moonLight.lightUnit=LightUnit.Lux;moonLight.intensity=intensityLux;
    }
    public void DrawControls()
    {
        GUILayout.Space(9);GUILayout.Label("MOON");
        GUILayout.Label($"Elevation angle: {elevation:0}°");
        float e=GUILayout.HorizontalSlider(elevation,5,85);
        GUILayout.Label($"Compass direction: {azimuth:0}°");
        float a=GUILayout.HorizontalSlider(azimuth,0,359.9f);
        GUILayout.Label($"Light intensity: {intensityLux:0.00} lux");
        float i=GUILayout.HorizontalSlider(intensityLux,0,1);
        if(e!=elevation||a!=azimuth||i!=intensityLux){elevation=e;azimuth=a;intensityLux=i;Apply();}
        if(GUILayout.Button("Reset moon",GUILayout.Height(23))){elevation=45;azimuth=0;intensityLux=.15f;Apply();}
        GUILayout.Label(moonLight!=null&&moonLight.enabled?"Esc to adjust • L switches moonlight":"Moonlight disabled by L");
    }
}
