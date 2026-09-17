using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class ChildFirstPersonController : MonoBehaviour
{
    [Header("Child proportions (metres)")]
    [Range(.8f,1.5f)] public float standingHeight=1.10f;
    public float standingEyeHeight=.95f;
    public float crouchingHeight=.70f;
    public float crouchingEyeHeight=.56f;
    [Header("Movement")]
    public float walkSpeed=1.65f;
    public float sprintSpeed=2.9f;
    public float crouchSpeed=.8f;
    public float acceleration=10f;
    public float maximumStepHeight=.38f;
    public float jumpHeight=.38f;
    public float gravity=20f;
    public float sprintSeconds=4f;
    public float staminaRecoverySeconds=5f;
    [Header("View")]
    public Camera playerCamera;
    public Light flashlight;
    public float mouseSensitivity=.085f;
    public float verticalFieldOfView=62f;
    public bool gentleHeadBob=false;
    public bool showControls=true;

    public bool InputEnabled { get; set; }=true;
    public bool IsCrouching { get; private set; }
    public bool IsSprinting { get; private set; }
    public float Stamina01 { get; private set; }=1;
    public CharacterController Body { get { EnsureInitialized(); return body; } }

    CharacterController body;
    readonly Collider[] overlaps=new Collider[32];
    Vector3 horizontalVelocity, spawn;
    Quaternion spawnRotation;
    float verticalVelocity, pitch, eyeHeight, bobPhase, recoveryDelay;
    bool exhausted, initialized;
    GUIStyle helpStyle;

    void Awake() { EnsureInitialized(); }
    void Start() { if(InputEnabled) CaptureMouse(); }
    void EnsureInitialized()
    {
        if(initialized) return;
        body=GetComponent<CharacterController>();
        body.height=standingHeight; body.radius=.19f;
        body.center=Vector3.up*standingHeight*.5f;
        body.skinWidth=.025f; body.stepOffset=maximumStepHeight; body.slopeLimit=48;
        body.minMoveDistance=0;
        eyeHeight=standingEyeHeight; spawn=transform.position; spawnRotation=transform.rotation;
        if(playerCamera!=null) {playerCamera.fieldOfView=verticalFieldOfView;playerCamera.nearClipPlane=.03f;}
        initialized=true;
    }
    void Update()
    {
        if(!InputEnabled) return;
        var keyboard=Keyboard.current; var mouse=Mouse.current;
        if(keyboard!=null && keyboard.escapeKey.wasPressedThisFrame) ReleaseMouse();
        if(Cursor.lockState!=CursorLockMode.Locked)
        {
            if(mouse!=null && mouse.leftButton.wasPressedThisFrame && Application.isFocused) CaptureMouse();
            SimulateInput(Vector2.zero,Vector2.zero,false,IsCrouching,false,Time.deltaTime);
            return;
        }
        if(keyboard!=null && keyboard.rKey.wasPressedThisFrame) { Respawn(); return; }
        if(keyboard!=null && keyboard.fKey.wasPressedThisFrame && flashlight!=null) flashlight.enabled=!flashlight.enabled;
        Vector2 move=Vector2.zero;
        if(keyboard!=null)
        {
            move.x=(keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0);
            move.y=(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0);
        }
        SimulateInput(move,mouse!=null?mouse.delta.ReadValue():Vector2.zero,
            keyboard!=null && keyboard.leftShiftKey.isPressed,
            keyboard!=null && (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed),
            keyboard!=null && keyboard.spaceKey.wasPressedThisFrame,Time.deltaTime);
    }
    // Shared movement path for keyboard input, scripted sequences, and deterministic editor checks.
    public void SimulateInput(Vector2 move,Vector2 mouseDelta,bool sprint,bool crouch,bool jump,float dt)
    {
        EnsureInitialized(); dt=Mathf.Clamp(dt,0,.05f); if(dt<=0) return;
        transform.Rotate(Vector3.up,mouseDelta.x*mouseSensitivity,Space.World);
        pitch=Mathf.Clamp(pitch-mouseDelta.y*mouseSensitivity,-80,80);
        IsCrouching=crouch || (IsCrouching && !CanStand());
        float targetHeight=IsCrouching?crouchingHeight:standingHeight;
        body.height=targetHeight; body.center=Vector3.up*targetHeight*.5f;
        body.stepOffset=maximumStepHeight;
        move=Vector2.ClampMagnitude(move,1);
        if(exhausted && Stamina01>=.3f) exhausted=false;
        IsSprinting=sprint && !IsCrouching && move.sqrMagnitude>.01f && !exhausted && Stamina01>0;
        if(IsSprinting)
        {
            Stamina01=Mathf.Max(0,Stamina01-dt/Mathf.Max(.1f,sprintSeconds)); recoveryDelay=1.2f;
            if(Stamina01<=0) {exhausted=true;IsSprinting=false;}
        }
        else
        {
            recoveryDelay=Mathf.Max(0,recoveryDelay-dt);
            if(recoveryDelay<=0) Stamina01=Mathf.Min(1,Stamina01+dt/Mathf.Max(.1f,staminaRecoverySeconds));
        }
        float speed=IsCrouching?crouchSpeed:IsSprinting?sprintSpeed:walkSpeed;
        Vector3 desired=(transform.right*move.x+transform.forward*move.y)*speed;
        horizontalVelocity=Vector3.MoveTowards(horizontalVelocity,desired,acceleration*dt);
        if(body.isGrounded && verticalVelocity<0) verticalVelocity=-2f;
        if(jump && body.isGrounded && !IsCrouching) verticalVelocity=Mathf.Sqrt(2*gravity*jumpHeight);
        verticalVelocity-=gravity*dt;
        var flags=body.Move((horizontalVelocity+Vector3.up*verticalVelocity)*dt);
        if((flags&CollisionFlags.Above)!=0 && verticalVelocity>0) verticalVelocity=0;
        if((flags&CollisionFlags.Below)!=0 && verticalVelocity<0) verticalVelocity=-2;
        if(transform.position.y < -15) Respawn();
        eyeHeight=Mathf.MoveTowards(eyeHeight,IsCrouching?crouchingEyeHeight:standingEyeHeight,2f*dt);
        float bob=0;
        if(gentleHeadBob && body.isGrounded && move.sqrMagnitude>.01f)
        {bobPhase+=dt*(IsSprinting?11:8);bob=Mathf.Sin(bobPhase)*.008f;}
        if(playerCamera!=null)
        {
            playerCamera.transform.localPosition=new Vector3(0,eyeHeight+bob,0);
            playerCamera.transform.localRotation=Quaternion.Euler(pitch,0,0);
        }
    }
    public bool CanStand()
    {
        EnsureInitialized();
        float r=body.radius*.95f;
        Vector3 foot=transform.position;
        int count=Physics.OverlapCapsuleNonAlloc(foot+Vector3.up*(body.radius+.05f),
            foot+Vector3.up*(standingHeight-body.radius),r,overlaps,~0,QueryTriggerInteraction.Ignore);
        if(count==overlaps.Length) return false;
        for(int i=0;i<count;i++) if(overlaps[i]!=null && !overlaps[i].transform.IsChildOf(transform)) return false;
        return true;
    }
    public void Teleport(Vector3 position,Quaternion rotation)
    {
        EnsureInitialized(); body.enabled=false;transform.SetPositionAndRotation(position,rotation);body.enabled=true;
        horizontalVelocity=Vector3.zero;verticalVelocity=0;pitch=0;
        Physics.SyncTransforms();
    }
    public void Respawn()
    { Teleport(spawn,spawnRotation);Stamina01=1;exhausted=false;recoveryDelay=0; }
    void CaptureMouse() {Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
    void ReleaseMouse() {Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    void OnApplicationFocus(bool focused) {if(!focused) ReleaseMouse();}
    void OnDisable() {ReleaseMouse();}
    void OnGUI()
    {
        if(!showControls || !InputEnabled) return;
        if(helpStyle==null) helpStyle=new GUIStyle(GUI.skin.label) {fontSize=14,alignment=TextAnchor.MiddleCenter};
        var old=GUI.color;GUI.color=new Color(0,0,0,.65f);GUI.DrawTexture(new Rect(0,Screen.height-42,Screen.width,42),Texture2D.whiteTexture);
        GUI.color=Color.white;
        string text=Cursor.lockState==CursorLockMode.Locked?
            "WASD  Move     Mouse  Look     Shift  Sprint     Ctrl / C  Crouch     Space  Small jump     F  Flashlight     R  Restart     Esc  Release mouse":
            "Click to play — explore the house through a child's eyes";
        GUI.Label(new Rect(8,Screen.height-39,Screen.width-16,32),text,helpStyle);
        if(Stamina01<.99f)
        {
            GUI.color=new Color(0,0,0,.55f);GUI.DrawTexture(new Rect(Screen.width/2f-60,Screen.height-58,120,4),Texture2D.whiteTexture);
            GUI.color=new Color(.8f,.85f,.85f,.85f);GUI.DrawTexture(new Rect(Screen.width/2f-60,Screen.height-58,120*Stamina01,4),Texture2D.whiteTexture);
        }
        GUI.color=old;
    }
}
