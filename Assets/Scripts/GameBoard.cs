using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;
using TMPro;
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
  
  [SerializeField] private bool pvp_mode = false;
  [SerializeField] public int starting_pieces_each = 10;
  private bool pvp_setup_active = false;
  private int current_placing_player = 1;
  private int placed_count_current_player = 0;

  private int player1_score = 0;
  private int player2_score = 0;

  private HashSet<Vector3Int> alive_cells;
  private HashSet<Vector3Int> cells_to_check;

  private Coroutine sim_coroutine;

  private int iterations_count = 0;
  [SerializeField] public int max_iterations = -1;

  [SerializeField] private TMP_Text announcer_text;

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
    if (in_edit_mode && pvp_setup_active) {
      if (Mouse.current.leftButton.wasPressedThisFrame) {
        Vector3 world_point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector3Int cell = current_state.WorldToCell(world_point);
        bool is_white = (current_placing_player == 1);
        if (current_state.GetTile(cell) == white_tile && is_white || 
            current_state.GetTile(cell) == red_tile && !is_white) {
          return;
        }
          
        SetAlive(cell, true, is_white);

        placed_count_current_player++;
        if (placed_count_current_player >= starting_pieces_each) {
          if (current_placing_player == 1) {
            current_placing_player = 2;
            placed_count_current_player = 0;
            announcer_text.SetText("Player 2 (red) place your pieces.");
          } else {
            FinishPvPSetup();
          }
        }
      }

      if (Mouse.current.rightButton.wasPressedThisFrame) {
        Vector3 world_point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        Vector3Int cell = current_state.WorldToCell(world_point);
        bool is_white = (current_state.GetTile(cell) == white_tile);
        if (is_white && current_state.GetTile(cell) != white_tile ||
            !is_white && current_state.GetTile(cell) != red_tile) {
          return;
        }
        
        SetAlive(cell, false, true);
        placed_count_current_player--;
      }

      return;
    }
    
    if (!pvp_mode && in_edit_mode && Mouse.current.leftButton.wasPressedThisFrame) {
      Vector3 world_point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
      Vector3Int cell = current_state.WorldToCell(world_point);
      ToggleCell(cell, true);
    }

    if (!pvp_mode && in_edit_mode && Mouse.current.rightButton.wasPressedThisFrame) {
      Vector3 world_point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
      Vector3Int cell = current_state.WorldToCell(world_point);
      ToggleCell(cell, true);
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

    iterations_count = 0;
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
      iterations_count += 1;
      interval = new WaitForSeconds(update_interval);
      yield return interval;
    }
  }

  public void StartPvPSetup(int piecesPerPlayer) {
    Clear();
    pvp_mode = true;
    pvp_setup_active = true;
    current_placing_player = 1;
    placed_count_current_player = 0;
    player1_score = 0;
    player2_score = 0;
    announcer_text.SetText("Player 1 (white) place your pieces.");
  }

  private void FinishPvPSetup() {
    pvp_setup_active = false;
    player1_score = 0;
    player2_score = 0;
    announcer_text.SetText("PVP setup Complete! Resume the game.");
  }

  public void EndGameManual() {
    EndGame();
  }

  private void EndGame() {
    PauseSimulation();
    pvp_setup_active = false;
    pvp_mode = false;
    string result;
    if (player1_score > player2_score) result = $"Player 1 (white) wins {player1_score} : {player2_score}";
    else if (player2_score > player1_score) result = $"Player 2 (red) wins {player2_score} : {player1_score}";
    else result = $"Draw {player1_score} : {player2_score}";
    announcer_text.SetText("Game Over! " + result);
    StartSimulation();
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
            bool bornWhite = soleIsWhite;
            next_state.SetTile(cell, bornWhite ? white_tile : red_tile);
            new_alive.Add(cell);
            if (pvp_mode) {
              if (bornWhite) player1_score++;
              else player2_score++;
            }
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
            bool bornWhite = alive_neighbors_white > alive_neighbors_red;
            if (bornWhite) next_state.SetTile(cell, white_tile);
            else next_state.SetTile(cell, red_tile);

            new_alive.Add(cell);

            if (pvp_mode) {
              if (bornWhite) player1_score++;
              else player2_score++;
            }
          }
        }
      }
    }

    Tilemap temp = current_state;
    current_state = next_state;
    next_state = temp;
    next_state.ClearAllTiles();

    alive_cells = new_alive;
    
    if (pvp_mode && alive_cells.Count == 0 || (max_iterations >= 0 && iterations_count >= max_iterations)) {
      EndGame();
    }
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