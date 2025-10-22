using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class GameBoard : MonoBehaviour {
  [SerializeField] private Tilemap current_state;
  [SerializeField] private Tilemap next_state;
  [SerializeField] private Tile white_tile;
  [SerializeField] private Tile red_tile;
  [SerializeField] private Pattern pattern;
  [SerializeField] private float update_interval = 0.05f;

  [SerializeField] private int random_field_width = 40;
  [SerializeField] private int random_field_height = 30;
  [SerializeField, Range(0f, 1f)] private float random_field_density = 0.2f;
  [SerializeField] public bool in_edit_mode = false;

  private HashSet<Vector3Int> alive_cells;
  private HashSet<Vector3Int> cells_to_check;

  private Coroutine sim_coroutine;

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
    if (in_edit_mode && Mouse.current.leftButton.wasPressedThisFrame) {
      Vector3 world_point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
      Vector3Int cell = current_state.WorldToCell(world_point);
      ToggleCell(cell, true);
    }

    if (in_edit_mode && Mouse.current.rightButton.wasPressedThisFrame) {
      Vector3 world_point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
      Vector3Int cell = current_state.WorldToCell(world_point);
      ToggleCell(cell, false);
    }
  }

  private void SetPattern(Pattern pattern) {
    Clear();

    Vector2Int center = pattern.GetCenter();

    for (int i = 0; i < pattern.cells.Length; i++) {
      Vector3Int cell = (Vector3Int)(pattern.cells[i] - center);
      current_state.SetTile(cell, white_tile);
      alive_cells.Add(cell);
    }
  }

  public void Clear() {
    current_state.ClearAllTiles();
    next_state.ClearAllTiles();
  }

  public void TogglePlayPause() {
    if (!PauseMenu.is_paused) {
      PauseSimulation();
    }
    else {
      StartSimulation();
    }
  }

  public void StartSimulation() {
    if (!PauseMenu.is_paused) {
      return;
    }

    sim_coroutine = StartCoroutine(Simulate());
    PauseMenu.is_paused = false;
  }

  public void PauseSimulation() {
    if (PauseMenu.is_paused) {
      return;
    }

    if (sim_coroutine != null) {
      StopCoroutine(sim_coroutine);
    }

    sim_coroutine = null;
    PauseMenu.is_paused = true;
  }

  public void SetSpeed(float update_interval) {
    this.update_interval = update_interval;
    if (!PauseMenu.is_paused) {
      PauseSimulation();
      StartSimulation();
    }
  }

  public float GetUpdateInterval() {
    return update_interval;
  }

  public void Randomize() {
    Clear();

    Vector2Int center = new Vector2Int(random_field_width, random_field_height) / 2;

    for (int x = -center.x; x <= center.x; x++) {
      for (int y = -center.y; y <= center.y; y++) {
        if (Random.value <= random_field_density) {
          Vector3Int cell = new Vector3Int(x, y, 0);
          SetAlive(cell, true, Random.Range(0, 2) == 1);
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

    var new_alive = new HashSet<Vector3Int>();

    foreach (Vector3Int cell in cells_to_check) {
      (int alive_neighbors_white, int alive_neighbors_red) = CountAliveNeighbors(cell);
      int total_neighbors = alive_neighbors_white + alive_neighbors_red;
      bool currently_alive = IsAlive(cell);

      bool neighborsSingleColor = (alive_neighbors_white == 0 && alive_neighbors_red > 0) ||
                                  (alive_neighbors_red == 0 && alive_neighbors_white > 0);
      bool soleIsWhite = (alive_neighbors_white > 0 && alive_neighbors_red == 0);

      if (neighborsSingleColor) {
        if (currently_alive) {
          if (total_neighbors == 2 || total_neighbors == 3) {
            next_state.SetTile(cell, soleIsWhite ? white_tile : red_tile);
            new_alive.Add(cell);
          }
        }
        else {
          if (total_neighbors == 3) {
            next_state.SetTile(cell, soleIsWhite ? white_tile : red_tile);
            new_alive.Add(cell);
          }
        }
      }
      else {
        if (currently_alive) {
          bool is_white = current_state.GetTile(cell) == white_tile;
          if (is_white) {
            if (alive_neighbors_white == 2 || alive_neighbors_white == 3) {
              next_state.SetTile(cell, white_tile);
              new_alive.Add(cell);
            }
          }
          else {
            if (alive_neighbors_red == 2 || alive_neighbors_red == 3) {
              next_state.SetTile(cell, red_tile);
              new_alive.Add(cell);
            }
          }
        }
        else {
          if (total_neighbors == 3) {
            if (alive_neighbors_white > alive_neighbors_red) {
              next_state.SetTile(cell, white_tile);
            }
            else {
              next_state.SetTile(cell, red_tile);
            }

            new_alive.Add(cell);
          }
        }
      }
    }

    Tilemap temp = current_state;
    current_state = next_state;
    next_state = temp;
    next_state.ClearAllTiles();

    alive_cells = new_alive;
  }

  private (int, int) CountAliveNeighbors(Vector3Int cell) {
    int count_white = 0;
    int count_red = 0;
    for (int x = -1; x <= 1; x++) {
      for (int y = -1; y <= 1; y++) {
        if (x == 0 && y == 0) continue;

        Vector3Int neighbor = cell + new Vector3Int(x, y, 0);
        if (IsAlive(neighbor)) {
          if (current_state.GetTile(neighbor) == white_tile) {
            count_white++;
          }
          else {
            count_red++;
          }
        }
      }
    }

    return (count_white, count_red);
  }

  private bool IsAlive(Vector3Int cell) {
    return current_state.HasTile(cell);
  }

  public void SetAlive(Vector3Int cell, bool alive, bool is_white) {
    if (alive) {
      if (is_white) {
        current_state.SetTile(cell, white_tile);
      }
      else {
        current_state.SetTile(cell, red_tile);
      }

      alive_cells.Add(cell);
    }
    else {
      current_state.SetTile(cell, null);
      alive_cells.Remove(cell);
    }
  }

  private void ToggleCell(Vector3Int cell, bool is_white) {
    if (IsAlive(cell)) {
      SetAlive(cell, false, is_white);
    }
    else {
      SetAlive(cell, true, is_white);
    }
  }
}