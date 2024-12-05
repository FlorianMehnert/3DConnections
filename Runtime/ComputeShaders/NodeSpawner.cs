using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.ComputeShaders
{
    public class NodeSpawner : MonoBehaviour
    {
        public GameObject cubePrefab;
        public int poolSize = 10000;
        private GameObject[] _cubePool;
        public void Execute()
        {
            _cubePool = new GameObject[poolSize];
            for (var i = 0; i < poolSize; i++) 
            {
                _cubePool[i] = Instantiate(cubePrefab, transform);
                _cubePool[i].SetActive(true);
            }
        }

        
    }
}