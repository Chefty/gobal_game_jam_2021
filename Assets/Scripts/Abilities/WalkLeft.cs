﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WalkLeft", menuName = "Abilities/WalkLeft")]
public class WalkLeft : Ability
{
    public override void ActionForTile()
    {
        throw new NotImplementedException();
    }

    public override void RunAction()
    {
        if (Input.GetKeyUp(ActionKeycode))
        {
            Vector3 newpos = GameManager.Instance.Player.position + (Vector3.left);

            if (GameManager.Instance.GetTileAccessibility(newpos))
            {
                GameManager.Instance.playerMovement.targetPosition += Vector3.left;
                GameManager.Instance.playerMovement.targetRotation = Quaternion.Euler(Vector3.left);
                GameManager.Instance.playerMovement.currentState = eState.walk;
            }
        }
    }
}
