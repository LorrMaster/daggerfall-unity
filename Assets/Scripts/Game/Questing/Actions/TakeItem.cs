﻿// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2018 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    
// 
// Notes:
//

using UnityEngine;
using System.Text.RegularExpressions;
using FullSerializer;

namespace DaggerfallWorkshop.Game.Questing.Actions
{
    /// <summary>
    /// Remove a quest Item from player.
    /// </summary>
    public class TakeItem : ActionTemplate
    {
        Symbol itemSymbol;

        public override string Pattern
        {
            get { return @"take (?<anItem>[a-zA-Z0-9_.]+) from pc"; }
        }

        public TakeItem(Quest parentQuest)
            : base(parentQuest)
        {
        }

        public override IQuestAction CreateNew(string source, Quest parentQuest)
        {
            // Source must match pattern
            Match match = Test(source);
            if (!match.Success)
                return null;

            // Factory new action
            TakeItem action = new TakeItem(parentQuest);
            action.itemSymbol = new Symbol(match.Groups["anItem"].Value);

            return action;
        }

        public override void Update(Task caller)
        {
            base.Update(caller);

            // Attempt to get Item resource
            Item item = ParentQuest.GetItem(itemSymbol);
            if (item == null)
            {
                Debug.LogErrorFormat("Could not find Item resource symbol {0}", itemSymbol);
                return;
            }

            // Release item - this will clear equip state and remove item from player's inventory
            // Now item is not associated with any collections and will just be garbage collected
            GameManager.Instance.PlayerEntity.ReleaseQuestItemForReoffer(item.DaggerfallUnityItem);

            SetComplete();
        }

        #region Serialization

        [fsObject("v1")]
        public struct SaveData_v1
        {
            public Symbol itemSymbol;
        }

        public override object GetSaveData()
        {
            SaveData_v1 data = new SaveData_v1();
            data.itemSymbol = itemSymbol;

            return data;
        }

        public override void RestoreSaveData(object dataIn)
        {
            SaveData_v1 data = (SaveData_v1)dataIn;
            if (dataIn == null)
                return;

            itemSymbol = data.itemSymbol;
        }

        #endregion
    }
}