using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HoverEffect : MonoBehaviour
{
    public void OnHoverEnterEffect(GameObject go)
    {
        go.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        
    }
    public void OnHoverExitEfeect(GameObject go)
    {
        go.transform.localScale = Vector3.one;
    }
}
