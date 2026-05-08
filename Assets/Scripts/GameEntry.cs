using Game.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Game
{


    public class GameEntry : MonoBehaviour
    {
        private void Start()
        {
            Initialize().Forget();
        }

        private async Task Initialize()
        {
            await ResourceManager.Instance.InitializeAsync();
             MapManager.Instance.Initialize();

            MapManager.Instance.LoadMap(1);
        }
    }

}