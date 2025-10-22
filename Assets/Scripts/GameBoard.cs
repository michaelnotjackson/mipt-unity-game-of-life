using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class GameBoard : MonoBehaviour {
  [SerializeField] private Tilemap current_state;
  [SerializeField] private Tilemap next_state;
  [SerializeField] private Tile alive_tile;
  [SerializeField] private Tile dead_tile;
  [SerializeField] private Pattern pattern;
  [SerializeField] private float update_interval = 0.05f;

  [SerializeField] private int random_field_width = 40;
  [SerializeField] private int random_field_height = 30;
  [SerializeField, Range(0f, 1f)] private float random_field_density = 0.2f;

  private HashSet<Vector3Int> alive_cells;
  private HashSet<Vector3Int> cells_to_check;

  private Coroutine sim_coroutine;
  [SerializeField] private bool is_running = false;

  private void Awake() {
    alive_cells = new HashSet<Vector3Int>();
    cells_to_check = new HashSet<Vector3Int>();
  }

  private void Start() {
    if (pattern != null) {
      SetPattern(pattern);
    }
  }

  private void Update() {
    if (!is_running && Mouse.current.leftButton.wasPressedThisFrame) {
      Vector3 world_point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
      Vector3Int cell = current_state.WorldToCell(world_point);
      ToggleCell(cell);
    }
    
    if (Keyboard.current.spaceKey.wasPressedThisFrame) {
      TogglePlayPause();
      Debug.Log("space pressed, is_running: " + is_running);
    }
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

  public void TogglePlayPause() {
    if (is_running) {
      PauseSimulation();
    }
    else {
      StartSimulation();
    }
  }

  public void StartSimulation() {
    if (is_running) {
      return;
    }
    
    sim_coroutine = StartCoroutine(Simulate());
    is_running = true;
  }
  
  public void PauseSimulation() {
    if (!is_running) {
      return;
    }

    if (sim_coroutine != null) {
      StopCoroutine(sim_coroutine);
    }
    sim_coroutine = null;
    is_running = false;
  }

  public void SetSpeed(float update_interval) {
    this.update_interval = update_interval;
    if (is_running) {
      PauseSimulation();
      StartSimulation();
    }
  }

  public void Randomize() {
    Clear();
    
    Vector2Int center = new Vector2Int(random_field_width, random_field_height) / 2;

    for (int x = -center.x; x <= center.x; x++) {
      for (int y = -center.y; y <= center.y; y++) {
        if (Random.value <= random_field_density) {
          Vector3Int cell = new Vector3Int(x, y, 0);
          SetAlive(cell, true);
        }
      }
    }
  }

  private IEnumerator Simulate() {
    var interval = new WaitForSeconds(update_interval);
    yield return interval;
    while (enabled) {
      UpdateState();
      interval = new WaitForSeconds(update_interval);
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
        }
        else {
          next_state.SetTile(cell, alive_tile);
        }
      }
      else {
        if (alive_neighbors == 3) {
          next_state.SetTile(cell, alive_tile);
          alive_cells.Add(cell);
        }
        else {
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

  public void SetAlive(Vector3Int cell, bool alive) {
    if (alive) {
      current_state.SetTile(cell, alive_tile);
      alive_cells.Add(cell);
    }
    else {
      current_state.SetTile(cell, dead_tile);
      alive_cells.Remove(cell);
    }
  }
  
  private void ToggleCell(Vector3Int cell) {
    if (IsAlive(cell)) {
      SetAlive(cell, false);
    }
    else {
      SetAlive(cell, true);
    }
  }
}