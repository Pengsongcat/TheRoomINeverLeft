using System;
using System.Collections.Generic;
using UnityEngine;

public class ActiveCopyer : MonoBehaviour
{
    [Serializable]
    public class ActivePair
    {
        [Tooltip("被跟随的源物体 (A / B / C ...)")]
        public GameObject source;

        [Tooltip("跟随源物体 active 状态的物体 (1 / 2 / 3 ...)")]
        public GameObject follower;

        [Tooltip("勾选后 follower 取反：source 为 true 时 follower 为 false")]
        public bool invert;
    }

    [Tooltip("下拉列表选择的配对数量")]
    public int pairCount = 1;

    [Tooltip("每一项：follower 跟随 source 的 active 状态")]
    public List<ActivePair> pairs = new();

    void Start()
    {
        Apply();
    }

    void Update()
    {
        Apply();
    }

    void Apply()
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            ActivePair pair = pairs[i];
            if (pair == null || pair.source == null || pair.follower == null)
                continue;

            bool target = pair.invert ? !pair.source.activeSelf : pair.source.activeSelf;
            if (pair.follower.activeSelf != target)
                pair.follower.SetActive(target);
        }
    }

    // 让 pairs 列表长度与 pairCount 一致（编辑器与运行时都可调用）
    public void ResizePairs()
    {
        pairCount = Mathf.Max(0, pairCount);

        while (pairs.Count < pairCount)
            pairs.Add(new ActivePair());
        while (pairs.Count > pairCount)
            pairs.RemoveAt(pairs.Count - 1);
    }

    void OnValidate()
    {
        ResizePairs();
    }
}
