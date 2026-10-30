using UnityEngine;

namespace HouseCreators.Blockout
{
    // Линия задуманного маршрута игрока для макета (видна в окне Scene).
    public class BlockoutRoute : MonoBehaviour
    {
        public Color color = Color.white;
        public Vector3[] points = new Vector3[0];

        void OnDrawGizmos()
        {
            if (points == null || points.Length < 2) return;
            Gizmos.color = color;
            for (int i = 0; i < points.Length; i++)
            {
                Gizmos.DrawSphere(points[i], 0.5f);
                if (i > 0) Gizmos.DrawLine(points[i - 1], points[i]);
            }
        }
    }
}
