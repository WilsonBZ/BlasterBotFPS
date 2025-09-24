//using System.Collections.Generic;
//using System;
//using UnityEngine;

//[Serializable]
//public struct RoomSocket
//{
//    public Vector2Int localGridPos;
//    public Direction orientation; 
//    public string id; 
//}

//[DisallowMultipleComponent]
//public class RoomAuthoring : MonoBehaviour
//{
//    [Tooltip("Room footprint size in grid cells. The room's root transform should be located at the bottom-left corner of this rectangle.")]
//    public Vector2Int size = new Vector2Int(3, 3);

//    [Tooltip("Sockets are (Doors), Connecting rooms and hallways")]
//    public List<RoomSocket> sockets = new List<RoomSocket>();

//#if UNITY_EDITOR
//    private void OnDrawGizmosSelected()
//    {
//        Gizmos.color = Color.green;
//        Vector3 baseWorld = transform.position;
//        float cell = 1f; 
//        Vector3 sizeWorld = new Vector3(size.x * cell, 0.01f, size.y * cell);
//        Gizmos.DrawWireCube(baseWorld + new Vector3(sizeWorld.x / 2f, 0, sizeWorld.z / 2f), sizeWorld);

//        Gizmos.color = Color.cyan;
//        foreach (var s in sockets)
//        {
//            Vector3 sockPos = baseWorld + new Vector3((s.localGridPos.x + 0.5f) * cell, 0.01f, (s.localGridPos.y + 0.5f) * cell);
//            Gizmos.DrawSphere(sockPos, 0.12f);
//            UnityEditor.Handles.Label(sockPos + Vector3.up * 0.2f, s.orientation.ToString() + " " + s.id);
//        }
//    }
//#endif
//}
