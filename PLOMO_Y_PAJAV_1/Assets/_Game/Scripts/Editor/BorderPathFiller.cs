using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fase N1.1: llena un BorderPath con piezas del kit siguiendo la regla N5 (no más de ~8 m de la misma pieza seguida).
/// Las piezas de cada tramo se estiran un poco para cubrirlo exacto y se solapan 0.1 m: no queda rendija entre colliders.
/// </summary>
public static class BorderPathFiller
{
    private const float MaxSameRun = 8f;
    private const float Overlap = 0.1f;

    /// <summary>Borra lo que tuviera el camino y lo vuelve a llenar. Devuelve cuántas piezas puso.</summary>
    public static int Fill(BorderPath path, System.Text.StringBuilder report)
    {
        for (int i = path.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(path.transform.GetChild(i).gameObject);

        var rng = new System.Random(path.Seed);
        Vector3[] points = path.Points;
        string last = null;
        float run = 0f;
        int order = 0, placed = 0;

        for (int s = 0; s + 1 < points.Length; s++)
        {
            Vector3 delta = points[s + 1] - points[s];
            delta.y = 0f;
            float length = delta.magnitude;
            if (length < 0.05f) continue;
            Vector3 dir = delta / length;
            Quaternion rot = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up); // +X local = a lo largo del tramo

            List<string> sequence = Choose(path, length, rng, ref last, ref run, ref order);
            float sum = 0f;
            foreach (string id in sequence) sum += BoundaryKitBuilder.Length(id);
            float stretch = length / Mathf.Max(0.01f, sum);

            float cursor = 0f;
            foreach (string id in sequence)
            {
                GameObject prefab = BoundaryKitBuilder.Load(id);
                float pieceLength = BoundaryKitBuilder.Length(id);
                float span = pieceLength * stretch;
                if (prefab == null)
                {
                    report.AppendLine($"✗ {path.name}: falta el prefab {id} (corre Crear kit de bordes).");
                    cursor += span;
                    continue;
                }
                var piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab, path.transform);
                piece.name = $"{id}_{placed}";
                piece.transform.SetPositionAndRotation(points[s] + dir * (cursor + span * 0.5f), rot);
                piece.transform.localScale = new Vector3((span + Overlap) / pieceLength, 1f, 1f);
                cursor += span;
                placed++;
            }
        }
        return placed;
    }

    private static List<string> Choose(BorderPath path, float length, System.Random rng, ref string last, ref float run, ref int order)
    {
        var chosen = new List<string>();
        string[] pool = path.Pieces;
        if (pool == null || pool.Length == 0) return chosen;

        float smallest = float.MaxValue;
        foreach (string id in pool) smallest = Mathf.Min(smallest, BoundaryKitBuilder.Length(id));

        float sum = 0f;
        var candidates = new List<string>();
        while (length - sum > 0.25f)
        {
            // Lo que falta es poco: las piezas puestas se estiran y lo cubren
            if (chosen.Count > 0 && length - sum < smallest * 0.35f) break;

            string pick;
            if (path.InOrder)
            {
                pick = pool[order % pool.Length];
                order++;
            }
            else
            {
                float remaining = length - sum;
                candidates.Clear();
                foreach (string id in pool)
                {
                    float l = BoundaryKitBuilder.Length(id);
                    if (l > remaining * 1.25f) continue;              // no entra ni comprimida
                    if (id == last && run + l > MaxSameRun) continue; // regla N5
                    candidates.Add(id);
                }
                if (candidates.Count == 0)
                {
                    // Tramo corto: la pieza más chica que no repita; si no hay, la más chica sin más
                    float best = float.MaxValue;
                    pick = pool[0];
                    foreach (string id in pool)
                    {
                        float l = BoundaryKitBuilder.Length(id) + (id == last ? 100f : 0f);
                        if (l < best) { best = l; pick = id; }
                    }
                }
                else pick = candidates[rng.Next(candidates.Count)];
            }

            float pieceLength = BoundaryKitBuilder.Length(pick);
            chosen.Add(pick);
            sum += pieceLength;
            run = pick == last ? run + pieceLength : pieceLength;
            last = pick;
        }
        return chosen;
    }
}
