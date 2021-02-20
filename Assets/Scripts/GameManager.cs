﻿using System.Linq;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using System;
using System.Collections;
using UnityEngine.SceneManagement;
using DG.Tweening;

[Serializable]
public class TileAbilityPair
{
    public Tile tileWithAbility;
    public Ability Ability;
}

[Serializable]
public class StartInfos
{
    public List<TileAbilityPair> StartPairs;
    public Vector3 PlayerStartPosition;
    public List<Ability> StartAbilities;
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public Inventory inventory;
    public Transform Player;
    private MultipleTargetsCamera camPlayer;
    private MultipleTargetsCamera camUI;

    public PlayerMovement playerMovement;
    public Transform mapRoot;
    public Action onDieOnLava;
    public Action onMoving;

    public float MapRotationSpeed = .5f;

    public int MaxAmountOfAbilities;
    public List<Ability> PlayerAbilities;
    public LayerMask mask;

    public Tile _currentTile;
    public Tile _prevTile;

    public StartInfos _levelAwakeState;

    [SerializeField] Image FadeBlack;
    public float FadeDuration = 1f;
    public Canvas deathScreen;
    public Canvas endScreen;

    public bool isDead = false;
    Bounds _mapBounds;

    private void Awake()
    {
        Instance = this;

        camPlayer = GameObject.Find("Main Camera").GetComponent<MultipleTargetsCamera>();
        camUI = GameObject.Find("WorldUI Camera").GetComponent<MultipleTargetsCamera>();

        camUI.offset = camPlayer.offset;
    }

    private void Start()
    {
        PrepareLoadLevel();
        CopyScriptableObjects();
        // we save the level infos
        RegisterLevelStartInformations();


        if (inventory == null)
        {
            Debug.LogError("Please fill the Inventory variable in GameManager.");
        }
        if (Player == null)
        {
            Debug.LogError("Please fill the Player variable in GameManager.");
        }
        if (mapRoot == null)
        {
            Debug.LogError("Please fill the map root variable in GameManager.");
        }

        FillUI();
        GetMapBounds();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            StartCoroutine(AsynReloadLevel());
            // restart the level here
            //LevelFlush();
            //LevelReload();
        }
        if (isDead)
        {
            return;
        }

        if (Player.gameObject.activeSelf)
        {
            for (int i = 0; i < PlayerAbilities.Count; i++)
            {
                PlayerAbilities[i].RunAction();
            }
        }

    }

    #region LevelStartup

    private void    GetMapBounds()
    {
        _mapBounds = new Bounds();
        var tiles = FindObjectsOfType<Tile>();

        for (int i = 0; i < tiles.Length; i++)
        {
            _mapBounds.Encapsulate(tiles[i].transform.position);
        }
    }

    private void CopyScriptableObjects()
    {
        for (int i = 0; i < PlayerAbilities.Count; i++)
        {
            PlayerAbilities[i] = Instantiate(PlayerAbilities[i]);
        }
    }

    #endregion

    public void DisplayDeathScreen()
    {
        deathScreen.enabled = true;
    }

    public void AddSlot()
    {
        MaxAmountOfAbilities += 1;
        inventory.InitInventorySlots();
    }

    private void FillUI()
    {
        for (int i = 0; i < PlayerAbilities.Count; i++)
        {
            inventory.AddAbility(PlayerAbilities[i]);
        }
    }

    public bool GetTileAccessibility(Vector3 pos)
    {
        Tile tile = GetTile(pos);

        if (tile != null)
        {
            return tile.CheckTileAccessibility();
        }

        return false;
    }

    public bool AddAbility(Ability newAbility)
    {
        if (PlayerAbilities.Count < MaxAmountOfAbilities)
        {
            if (!PlayerAbilities.Contains(newAbility))
            {
                PlayerAbilities.Add(newAbility);

                //TODO pas ouf à refacto
                _currentTile.TileOwnAbility.AbilityTaken();
                _currentTile.TileOwnAbility = null;

                _currentTile.DebugDisplay();

                CheckForCurrentTileAbility();

                // did take the ability
                return true;
            }
        }

        // didn't take the ability
        return false;
    }

    public void SwapAbility(Ability UIAbility)
    {
        Ability newAbility = _currentTile.TileOwnAbility;

        _currentTile.TileOwnAbility = UIAbility;
        _currentTile.DisplayAbility();

        PlayerAbilities.Remove(UIAbility);
        PlayerAbilities.Add(newAbility);

        newAbility.AbilityTaken();

        CheckForCurrentTileAbility();
    }

    public void DumpAbility(Ability ability)
    {
        if (!PlayerAbilities.Contains(ability))
        {
            return;
        }

        Tile tile = GetTile(Player.transform.position);

        if (tile.TileOwnAbility != null)
        {
            return;
        }

        tile.TileOwnAbility = ability;
        tile.DisplayAbility();

        PlayerAbilities.Remove(ability);

        CheckForCurrentTileAbility();
    }

    public void DestroyAbility(Ability ability)
    {
        PlayerAbilities.Remove(ability);

        inventory.RemoveAbility(ability);
    }

    public Ability GetFirstAbility(Type typeOfAbility)
    {
        return PlayerAbilities.Where(x => x.GetType() == typeOfAbility).FirstOrDefault();
    }

    public Tile GetTile(Vector3 pos)
    {
        RaycastHit m_Hit;

        if (Physics.Raycast(pos + (Vector3.up * 15f), Vector3.down, out m_Hit, 50f, mask))
        {
            //print("found " + m_Hit.collider.name + " [" + m_Hit.collider.GetComponent<Tile>().GetType().ToString() + "] at " + pos);
            return m_Hit.collider.GetComponent<Tile>();
        }

        return null;
    }

    public bool DoesPlayerPosessAbility(Type type)
    {
        for (int i = 0; i < PlayerAbilities.Count; i++)
        {
            if (PlayerAbilities[i].GetType() == type)
            {
                return true;
            }
        }

        return false;
    }

    public void SetCurrentTile(Tile newTile)
    {
        _prevTile = _currentTile;
        _currentTile = newTile;

        CheckForCurrentTileAbility();
    }

    private void CheckForCurrentTileAbility()
    {
        // if any ability available on the cell
        // display swipe UI
        if (_currentTile.TileOwnAbility != null)
        {
            inventory.ShowHideSwapUI(true, _currentTile.TileOwnAbility);
        }
        else
        {
            inventory.ShowHideSwapUI(false, null);
        }
    }

    #region Map rotation

    [ContextMenu("Rotate level")]
    private void DebugRotate()
    {
        RotateLevel(-1f);
    }

    public void RotateLevel(float AxisOrientation)
    {
        StartCoroutine(SmoothRotateMap(AxisOrientation));
        RotateAbilities(AxisOrientation);
    }

    IEnumerator SmoothRotateMap(float axisOrientation)
    {
        float time = 0;
        Quaternion fromAngle = mapRoot.rotation;
        Quaternion toAngle = Quaternion.Euler(mapRoot.eulerAngles + (Vector3.up * axisOrientation * 90f));

        while (playerMovement.isLerping) { yield return new WaitForEndOfFrame(); }

        //Player.parent = _currentTile.transform;
        Vector3 originOffset = camPlayer.offset;

        while (time < MapRotationSpeed)
        {
            camPlayer.offset = Vector3.Lerp(
                originOffset,
                new Vector3(originOffset.z * -axisOrientation, originOffset.y, originOffset.x / 2f),
                MapRotationSpeed / time);
            //mapRoot.RotateAround(_mapBounds.center, Vector3.up, (90f * axisOrientation) * Time.deltaTime);

            time += Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        camUI.offset = camPlayer.offset;


        //Player.parent = null;

        yield return null;
    }

    Vector3 ConvertDirection(Vector3 fromDirection, float AxisOrientation)
    {
        Vector3 newdirection = Vector3.zero;

        if (fromDirection == Vector3.left)
        {
            if (AxisOrientation == -1f)
            {
                newdirection = Vector3.forward;
            }
            else
            {
                newdirection = Vector3.back;
                //((Walk)tiles[i].Ability).UpdateDirection(Vector3.forward);
            }
        }
        else if (fromDirection == Vector3.right)
        {
            if (AxisOrientation == -1f)
            {
                newdirection = Vector3.back;

                //((Walk)tiles[i].Ability).UpdateDirection(Vector3.forward);
            }
            else
            {
                newdirection = Vector3.forward;

                //((Walk)tiles[i].Ability).UpdateDirection(Vector3.back);
            }
        }
        else if (fromDirection == Vector3.forward)
        {
            if (AxisOrientation == -1f)
            {
                newdirection = Vector3.right;

                //((Walk)tiles[i].Ability).UpdateDirection(Vector3.left);
            }
            else
            {
                newdirection = Vector3.left;
            }
        }
        else if (fromDirection == Vector3.back)
        {
            if (AxisOrientation == -1f)
            {
                newdirection = Vector3.left;
            }
            else
            {
                newdirection = Vector3.right;
            }
        }

        return newdirection;
    }

    private void RotateAbilities(float AxisOrientation)
    {
        var tiles = GetTilesAndTheirAbilities();
        Vector3 fromDirection;
        Vector3 newdirection = Vector3.zero;

        for (int i = 0; i < PlayerAbilities.Count; i++)
        {
            if (PlayerAbilities[i].GetType() == typeof(Walk))
            {
                fromDirection = ((Walk)PlayerAbilities[i]).WalkDirection;
                newdirection = Vector3.zero;
                newdirection = ConvertDirection(fromDirection, AxisOrientation);
                //PlayerAbilities[i].AbilityIcon = AbiltiesRotatedAssets.Instance.GetWalkArrowFromRotation(newdirection);
                //inventory.abilityItems[i].abilityIcon.sprite = PlayerAbilities[i].AbilityIcon;

                ((Walk)PlayerAbilities[i]).WalkDirection = newdirection;
            }
        }

        AxisOrientation *= -1f;

        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].Ability.GetType() == typeof(Walk))
            {
                fromDirection = ((Walk)tiles[i].Ability).WalkDirection;
                newdirection = ConvertDirection(fromDirection, AxisOrientation);
                ((Walk)tiles[i].Ability).UpdateDirection(newdirection);
            }
        }

        /*
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].Ability.GetType() == typeof(Walk))
            {
                fromDirection = ((Walk)tiles[i].Ability).WalkDirection;
                Vector3 newdirection = Vector3.zero;
                if (fromDirection == Vector3.left)
                {
                    if (AxisOrientation == -1f)
                    {
                        newdirection = Vector3.back;
                    }
                    else
                    {
                        newdirection = Vector3.forward;
                        //((Walk)tiles[i].Ability).UpdateDirection(Vector3.forward);
                    }
                }
                else if (fromDirection == Vector3.right)
                {
                    if (AxisOrientation == -1f)
                    {
                        newdirection = Vector3.up;

                        //((Walk)tiles[i].Ability).UpdateDirection(Vector3.up);
                    }
                    else
                    {
                        newdirection = Vector3.back;

                        //((Walk)tiles[i].Ability).UpdateDirection(Vector3.back);
                    }
                }
                else if (fromDirection == Vector3.up)
                {
                    if (AxisOrientation == -1f)
                    {
                        newdirection = Vector3.left;

                        //((Walk)tiles[i].Ability).UpdateDirection(Vector3.left);
                    }
                    else
                    {
                        newdirection = Vector3.right;

                        //((Walk)tiles[i].Ability).UpdateDirection(Vector3.right);
                    }
                }
                else if (fromDirection == Vector3.down)
                {
                    if (AxisOrientation == -1f)
                    {
                        newdirection = Vector3.right;

                        //((Walk)tiles[i].Ability).UpdateDirection(Vector3.right);
                    }
                    else
                    {
                        newdirection = Vector3.left;

                        //((Walk)tiles[i].Ability).UpdateDirection(Vector3.left);
                    }
                }



                ((Walk)tiles[i].Ability).UpdateDirection(Vector3.back);

            }
        }
         */

    }

    #endregion

    #region Level Flush

    private void LevelFlush()
    {
        FlushAllTilesWithAbility();
        FlushUI();
        FlushPlayerAbilities();
    }

    private void FlushPlayerAbilities()
    {
        PlayerAbilities.Clear();
    }

    private void FlushUI()
    {
        inventory.FlushUI();
    }

    private void FlushAllTilesWithAbility()
    {
        List<Tile> TilesWithAbilities = FindObjectsOfType<Tile>().Where(x => x.TileOwnAbility != null).ToList();

        TilesWithAbilities.ForEach(x =>
        {
            x.TileOwnAbility.AbilityTaken();
            x.TileOwnAbility = null;
            x.DebugDisplay();
        });        
    }

    #endregion

    #region Level reload

    public void PrepareReloadLevel()
    {
        StartCoroutine(AsynReloadLevel());
    }
    public void PrepareLoadLevel()
    {
        StartCoroutine(AsynLoadLevel());
    }

    public void PrepareLoadNextLevel()
    {
        StartCoroutine(AsyncLoadNextLevel());
    }

    IEnumerator AsynReloadLevel()
    {
        FadeBlack.DOFade(1f, FadeDuration/8f).SetEase(Ease.OutExpo);

        yield return new WaitForSeconds(FadeDuration);

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    IEnumerator AsynLoadLevel()
    {
        FadeBlack.transform.parent.gameObject.SetActive(true);

        FadeBlack.color = new Color(
            FadeBlack.color.r,
            FadeBlack.color.g,
            FadeBlack.color.b,
            1f);
        FadeBlack.DOFade(0f, FadeDuration).SetEase(Ease.InCirc);

        yield return new WaitForSeconds(FadeDuration);

        FadeBlack.transform.parent.gameObject.SetActive(false);
    }

    IEnumerator AsyncLoadNextLevel()
    {
        FadeBlack.transform.parent.gameObject.SetActive(true);

        FadeBlack.DOFade(1f, FadeDuration).SetEase(Ease.OutExpo);

        yield return new WaitForSeconds(FadeDuration);

        if (SceneManager.GetActiveScene().buildIndex < SceneManager.sceneCount)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        else
            endScreen.enabled = true;

    }

    private void RePlacePlayer()
    {
        Player.position = _levelAwakeState.PlayerStartPosition;
        Player.gameObject.SetActive(true);
    }

    private void ReFillTilesWithAbility()
    {
        _levelAwakeState.StartPairs.ForEach(x =>
        {
            x.tileWithAbility.TileOwnAbility = x.Ability;
            x.tileWithAbility.DisplayAbility();
        });
    }

    private void ReFillPlayerAbilities()
    {
        PlayerAbilities = new List<Ability>(_levelAwakeState.StartAbilities);
    }

    #endregion

    #region Level Save

    private void RegisterLevelStartInformations()
    {
        _levelAwakeState = new StartInfos()
        {
            StartPairs = GetTilesAndTheirAbilities(),
            PlayerStartPosition = Player.transform.position,
            StartAbilities = new List<Ability>(PlayerAbilities)
        };
    }

    private List<TileAbilityPair> GetTilesAndTheirAbilities()
    {
        List<Tile> TilesWithAbilities = FindObjectsOfType<Tile>().Where(x => x.TileOwnAbility != null).ToList();
        List<TileAbilityPair> pairs = new List<TileAbilityPair>();

        TilesWithAbilities.ForEach(x => pairs.Add(new TileAbilityPair()
        {
            tileWithAbility = x,
            Ability = x.TileOwnAbility
        }));

        return pairs;
    }

    #endregion
}
