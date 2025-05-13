using System;
using UnityEngine;

namespace Data
{
    [CreateAssetMenu(fileName = "MoveData", menuName = "Scriptable Objects/MoveData")]
    public class MoveData : ScriptableObject
    {
        public string MoveName;
        public ElementType MoveType;
        public int MoveDamage;
    }
}
