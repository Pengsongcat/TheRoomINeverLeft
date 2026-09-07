using UnityEngine;

public class AutoTurnOffLight : MonoBehaviour
{
    [SerializeField] private bool turnOffChildrenOnStart;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (turnOffChildrenOnStart)
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
