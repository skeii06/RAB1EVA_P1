using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ForestMazeGrid : MonoBehaviour
{
    public int width;
    public int height;
    public float cellSize = 5.2f;
    public Vector3 firstCellCenter;
    public int[] passages;

    public void Configure(int mazeWidth, int mazeHeight, float size, Vector3 firstCenter, int[] mazePassages)
    {
        width = mazeWidth;
        height = mazeHeight;
        cellSize = size;
        firstCellCenter = firstCenter;
        passages = mazePassages;
    }

    public Vector3 CellCenter(Vector2Int cell)
    {
        return new Vector3(firstCellCenter.x + cell.x * cellSize, 0, firstCellCenter.z + cell.y * cellSize);
    }

    public Vector2Int WorldToCell(Vector3 position)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt((position.x - firstCellCenter.x) / cellSize), 0, width - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt((position.z - firstCellCenter.z) / cellSize), 0, height - 1);
        return new Vector2Int(x, y);
    }

    public bool TryGetNextCell(Vector3 from, Vector3 to, out Vector2Int nextCell)
    {
        var start = WorldToCell(from);
        var goal = WorldToCell(to);
        var path = FindPath(start, goal);
        if (path.Count > 1)
        {
            nextCell = path[1];
            return true;
        }
        nextCell = start;
        return path.Count == 1;
    }

    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        var result = new List<Vector2Int>();
        if (passages == null || passages.Length != width * height || width <= 0 || height <= 0) return result;
        int count = width * height;
        var parent = new int[count];
        Array.Fill(parent, -1);
        int startIndex = start.y * width + start.x;
        int goalIndex = goal.y * width + goal.x;
        var queue = new Queue<int>();
        parent[startIndex] = startIndex;
        queue.Enqueue(startIndex);
        int[] bits = { 1, 2, 4, 8 };
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (current == goalIndex) break;
            var cell = new Vector2Int(current % width, current / width);
            for (int d = 0; d < 4; d++)
            {
                if ((passages[current] & bits[d]) == 0) continue;
                var neighbor = cell + directions[d];
                if (neighbor.x < 0 || neighbor.x >= width || neighbor.y < 0 || neighbor.y >= height) continue;
                int neighborIndex = neighbor.y * width + neighbor.x;
                if (parent[neighborIndex] != -1) continue;
                parent[neighborIndex] = current;
                queue.Enqueue(neighborIndex);
            }
        }
        if (parent[goalIndex] == -1) return result;
        for (int at = goalIndex; at != startIndex; at = parent[at]) result.Add(new Vector2Int(at % width, at / width));
        result.Add(start);
        result.Reverse();
        return result;
    }
}
