using System.Collections.Generic;
using UnityEngine;

public static class BfsPathfinder
{
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0)
    };

    public static List<Vector2Int> FindPath(CityGenerator city, Vector2Int start, Vector2Int goal)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        if (!CanSearch(city, start) || !CanSearch(city, goal))
        {
            return path;
        }

        if (start == goal)
        {
            path.Add(start);
            return path;
        }

        int width = city.width;
        int height = city.height;
        bool[,] visited = new bool[width, height];
        Vector2Int[,] previous = new Vector2Int[width, height];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        visited[start.x, start.y] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (current == goal)
            {
                return ReconstructPath(start, goal, previous);
            }

            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2Int next = current + Directions[i];
                if (!CanSearch(city, next) || visited[next.x, next.y])
                {
                    continue;
                }

                visited[next.x, next.y] = true;
                previous[next.x, next.y] = current;
                queue.Enqueue(next);
            }
        }

        return path;
    }

    public static bool FindNearestReachable(
        CityGenerator city,
        Vector2Int start,
        List<Vector2Int> candidates,
        out Vector2Int target,
        out List<Vector2Int> path)
    {
        target = new Vector2Int(-1, -1);
        path = new List<Vector2Int>();

        if (!CanSearch(city, start) || candidates == null || candidates.Count == 0)
        {
            return false;
        }

        int width = city.width;
        int height = city.height;
        bool[,] visited = new bool[width, height];
        int[,] distance = new int[width, height];
        Vector2Int[,] previous = new Vector2Int[width, height];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                distance[x, y] = -1;
            }
        }

        visited[start.x, start.y] = true;
        distance[start.x, start.y] = 0;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2Int next = current + Directions[i];
                if (!CanSearch(city, next) || visited[next.x, next.y])
                {
                    continue;
                }

                visited[next.x, next.y] = true;
                distance[next.x, next.y] = distance[current.x, current.y] + 1;
                previous[next.x, next.y] = current;
                queue.Enqueue(next);
            }
        }

        int bestDistance = int.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2Int candidate = candidates[i];
            if (!city.IsInsideGrid(candidate.x, candidate.y))
            {
                continue;
            }

            int candidateDistance = distance[candidate.x, candidate.y];
            if (candidateDistance >= 0 && candidateDistance < bestDistance)
            {
                bestDistance = candidateDistance;
                target = candidate;
            }
        }

        if (bestDistance == int.MaxValue)
        {
            return false;
        }

        path = ReconstructPath(start, target, previous);
        return path.Count > 0;
    }

    private static bool CanSearch(CityGenerator city, Vector2Int position)
    {
        if (city == null || city.Grid == null || !city.IsInsideGrid(position.x, position.y))
        {
            return false;
        }

        CityCell cell = city.Grid[position.x, position.y];
        return cell != null && cell.walkable;
    }

    private static List<Vector2Int> ReconstructPath(Vector2Int start, Vector2Int goal, Vector2Int[,] previous)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int current = goal;
        path.Add(current);

        while (current != start)
        {
            current = previous[current.x, current.y];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
