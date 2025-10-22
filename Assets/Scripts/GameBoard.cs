using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

public class GameBoard : MonoBehaviour {
  [SerializeField] private Tilemap current_state;
  [SerializeField] private Tilemap next_state;
  [SerializeField] private Tile alive_tile;
  [SerializeField] private Tile dead_tile;
  [SerializeField] private Pattern pattern;
  [SerializeField] private float update_interval = 0.05f;

  private HashSet<Vector3Int> alive_cells;
  private HashSet<Vector3Int> cells_to_check;

  private void Awake() {
    alive_cells = new HashSet<Vector3Int>();
    cells_to_check = new HashSet<Vector3Int>();
  }

  private void Start() {
    SetPattern(pattern);
  }

  private void SetPattern(Pattern pattern) {
    Clear();

    Vector2Int center = pattern.GetCenter();

    for (int i = 0; i < pattern.cells.Length; i++) {
      Vector3Int cell = (Vector3Int)(pattern.cells[i] - center);
      current_state.SetTile(cell, alive_tile);
      alive_cells.Add(cell);
    }
  }

  private void Clear() {
    current_state.ClearAllTiles();
    next_state.ClearAllTiles();
  }

  private void OnEnable() {
    StartCoroutine(Simulate());
  }

  private IEnumerator Simulate() {
    var interval = new WaitForSeconds(update_interval);
    yield return interval;
    while (enabled) {
      UpdateState();
      yield return interval;
    }
  }

  private void UpdateState() {
    cells_to_check.Clear();

    foreach (Vector3Int cell in alive_cells) {
      for (int x = -1; x <= 1; x++) {
        for (int y = -1; y <= 1; y++) {
          cells_to_check.Add(cell + new Vector3Int(x, y, 0));
        }
      }
    }

    foreach (Vector3Int cell in cells_to_check) {
      int alive_neighbors = CountAliveNeighbors(cell);
      bool currently_alive = IsAlive(cell);

      if (currently_alive) {
        if (alive_neighbors < 2 || alive_neighbors > 3) {
          next_state.SetTile(cell, dead_tile);
          alive_cells.Remove(cell);
        } else {
          next_state.SetTile(cell, alive_tile);
        }
      } else {
        if (alive_neighbors == 3) {
          next_state.SetTile(cell, alive_tile);
          alive_cells.Add(cell);
        } else {
          next_state.SetTile(cell, dead_tile);
        }
      }
    }
    
    Tilemap temp = current_state;
    current_state = next_state;
    next_state = temp;
    next_state.ClearAllTiles();
  }

  private int CountAliveNeighbors(Vector3Int cell) {
    int count = 0;
    for (int x = -1; x <= 1; x++) {
      for (int y = -1; y <= 1; y++) {
        if (x == 0 && y == 0) continue;

        Vector3Int neighbor = cell + new Vector3Int(x, y, 0);
        if (IsAlive(neighbor)) {
          count++;
        }
      }
    }

    return count;
  }

  private bool IsAlive(Vector3Int cell) {
    return current_state.GetTile(cell) == alive_tile;
  }
}