using UnityEngine;
using UnityEngine.AI;
public sealed class StalkerBodyMotion : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform leftArm,rightArm,leftLeg,rightLeg,head;
    float phase;
    void LateUpdate()
    {
        if(agent==null)return;float speed=agent.velocity.magnitude;phase+=Time.deltaTime*speed*4;
        float stride=Mathf.Sin(phase)*Mathf.Clamp01(speed)*22;
        if(leftLeg!=null)leftLeg.localRotation=Quaternion.Euler(stride,0,0);
        if(rightLeg!=null)rightLeg.localRotation=Quaternion.Euler(-stride,0,0);
        if(leftArm!=null)leftArm.localRotation=Quaternion.Euler(-stride*.65f,0,-8);
        if(rightArm!=null)rightArm.localRotation=Quaternion.Euler(stride*.65f,0,8);
        if(head!=null)head.localRotation=Quaternion.Euler(5,Mathf.Sin(Time.time*.65f)*8,0);
    }
}
