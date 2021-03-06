// Project:         Daggerfall Tools For Unity
// Copyright:       Copyright (C) 2009-2018 Daggerfall Workshop
// Web Site:        http://www.dfworkshop.net
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Source Code:     https://github.com/Interkarma/daggerfall-unity
// Original Author: Gavin Clayton (interkarma@dfworkshop.net)
// Contributors:    Hazelnut
// 
// Notes:
//

using UnityEngine;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.Banking;

namespace DaggerfallWorkshop.Game.UserInterfaceWindows
{
    public class DaggerfallRestWindow : DaggerfallPopupWindow
    {
        #region UI Rects

        Rect whileButtonRect = new Rect(4, 13, 48, 24);
        Rect healedButtonRect = new Rect(53, 13, 48, 24);
        Rect loiterButtonRect = new Rect(102, 13, 48, 24);
        Rect counterPanelRect = new Rect(0, 50, 105, 41);
        Rect counterTextPanelRect = new Rect(4, 10, 16, 8);
        Rect stopButtonRect = new Rect(33, 26, 40, 10);

        #endregion

        #region UI Controls

        Button whileButton;
        Button healedButton;
        Button loiterButton;
        Button stopButton;

        Panel mainPanel = new Panel();
        Panel counterPanel = new Panel();

        TextLabel counterLabel = new TextLabel();

        #endregion

        #region UI Textures

        Texture2D baseTexture;
        Texture2D hoursPastTexture;
        Texture2D hoursRemainingTexture;

        #endregion

        #region Fields

        const string baseTextureName = "REST00I0.IMG";              // Rest type
        const string hoursPastTextureName = "REST01I0.IMG";         // "Hours past"
        const string hoursRemainingTextureName = "REST02I0.IMG";    // "Hours remaining"

        const float restWaitTimePerHour = 0.75f;
        const float loiterWaitTimePerHour = 1.25f;
        const int cityCampingIllegal = 17;

        RestModes currentRestMode = RestModes.Selection;
        int hoursRemaining = 0;
        int totalHours = 0;
        float waitTimer = 0;
        bool enemyBrokeRest = false;
        int remainingHoursRented = -1;
        Vector3 allocatedBed;

        PlayerEntity playerEntity;
        DaggerfallHUD hud;

        #endregion

        #region Enums

        enum RestModes
        {
            Selection,
            TimedRest,
            FullRest,
            Loiter,
        }

        #endregion

        #region Constructors

        public DaggerfallRestWindow(IUserInterfaceManager uiManager)
            : base(uiManager)
        {
        }

        #endregion

        #region Setup Methods

        protected override void Setup()
        {
            // Load all the textures used by rest interface
            LoadTextures();

            // Hide world while resting
            ParentPanel.BackgroundColor = Color.black;

            // Create interface panel
            mainPanel.HorizontalAlignment = HorizontalAlignment.Center;
            mainPanel.BackgroundTexture = baseTexture;
            mainPanel.Position = new Vector2(0, 50);
            mainPanel.Size = new Vector2(ImageReader.GetImageData("REST00I0.IMG", 0, 0, false, false).width, ImageReader.GetImageData("REST00I0.IMG", 0, 0, false, false).height);

            NativePanel.Components.Add(mainPanel);

            // Create buttons
            whileButton = DaggerfallUI.AddButton(whileButtonRect, mainPanel);
            whileButton.OnMouseClick += WhileButton_OnMouseClick;
            healedButton = DaggerfallUI.AddButton(healedButtonRect, mainPanel);
            healedButton.OnMouseClick += HealedButton_OnMouseClick;
            loiterButton = DaggerfallUI.AddButton(loiterButtonRect, mainPanel);
            loiterButton.OnMouseClick += LoiterButton_OnMouseClick;

            // Setup counter panel
            counterPanel.Position = new Vector2(counterPanelRect.x, counterPanelRect.y);
            counterPanel.Size = new Vector2(counterPanelRect.width, counterPanelRect.height);
            counterPanel.HorizontalAlignment = HorizontalAlignment.Center;
            counterPanel.Enabled = false;
            NativePanel.Components.Add(counterPanel);

            // Setup counter text
            Panel counterTextPanel = DaggerfallUI.AddPanel(counterTextPanelRect, counterPanel);
            counterLabel.Position = new Vector2(0, 2);
            counterLabel.HorizontalAlignment = HorizontalAlignment.Center;
            counterTextPanel.Components.Add(counterLabel);

            // Stop button
            stopButton = DaggerfallUI.AddButton(stopButtonRect, counterPanel);
            stopButton.OnMouseClick += StopButton_OnMouseClick;
        }

        #endregion

        #region Overrides

        public override void Update()
        {
            base.Update();

            // Update HUD
            if (hud != null)
            {
                hud.Update();
            }

            ShowStatus();
            if (currentRestMode != RestModes.Selection)
            {
                if ((currentRestMode == RestModes.FullRest) && IsPlayerFullyHealed())
                    EndRest();
                else if ((currentRestMode != RestModes.FullRest) && hoursRemaining < 1)
                    EndRest();
                else if (TickRest())
                    EndRest();
            }
        }

        public override void Draw()
        {
            base.Draw();

            // Draw vitals
            if (hud != null)
            {
                hud.HUDVitals.Draw();
            }
        }

        public override void OnPush()
        {
            base.OnPush();

            // Reset counters
            hoursRemaining = 0;
            totalHours = 0;
            waitTimer = 0;
            enemyBrokeRest = false;

            // Get references
            playerEntity = GameManager.Instance.PlayerEntity;
            hud = DaggerfallUI.Instance.DaggerfallHUD;
        }

        public override void OnPop()
        {
            base.OnPop();

            Debug.Log(string.Format("Resting raised time by {0} hours total", totalHours));
        }

        #endregion

        #region Private Methods

        void LoadTextures()
        {
            baseTexture = ImageReader.GetTexture(baseTextureName);
            hoursPastTexture = ImageReader.GetTexture(hoursPastTextureName);
            hoursRemainingTexture = ImageReader.GetTexture(hoursRemainingTextureName);
        }

        void ShowStatus()
        {
            // Display status based on current rest state
            if (currentRestMode == RestModes.Selection)
            {
                mainPanel.Enabled = true;
                counterPanel.Enabled = false;
            }
            else if (currentRestMode == RestModes.FullRest)
            {
                mainPanel.Enabled = false;
                counterPanel.Enabled = true;
                counterPanel.BackgroundTexture = hoursPastTexture;
                counterLabel.Text = totalHours.ToString();
            }
            else if (currentRestMode == RestModes.TimedRest)
            {
                mainPanel.Enabled = false;
                counterPanel.Enabled = true;
                counterPanel.BackgroundTexture = hoursRemainingTexture;
                counterLabel.Text = hoursRemaining.ToString();
            }
            else if (currentRestMode == RestModes.Loiter)
            {
                mainPanel.Enabled = false;
                counterPanel.Enabled = true;
                counterPanel.BackgroundTexture = hoursRemainingTexture;
                counterLabel.Text = hoursRemaining.ToString();
            }
        }

        bool TickRest()
        {
            // Do nothing if another window has taken over UI
            // This will stop rest from progressing further until player dismisses top window
            if (uiManager.TopWindow != this)
                return false;

            // Loitering runs at a slower rate to rest
            float waitTime = (currentRestMode == RestModes.Loiter) ? loiterWaitTimePerHour : restWaitTimePerHour;

            // Tick timer by rate and count based on rest type
            bool finished = false;
            if (Time.realtimeSinceStartup > waitTimer + waitTime)
            {
                waitTimer = Time.realtimeSinceStartup;

                // Progress world time by 1 hour and tick quest machine
                // This could cause enemies to be spawned
                totalHours++;
                DaggerfallUnity.WorldTime.Now.RaiseTime(DaggerfallDateTime.SecondsPerHour);
                Questing.QuestMachine.Instance.Tick();

                // Do nothing if another window (e.g. quest popup) has suddenly taken over UI
                // Checking for second time as quest tick above can perfectly align with rest ending
                if (uiManager.TopWindow != this)
                    return false;

                // TODO: Random spawns appropriate to player location

                // Check if enemies nearby
                if (GameManager.Instance.AreEnemiesNearby())
                {
                    enemyBrokeRest = true;
                    return true;
                }

                if (currentRestMode == RestModes.TimedRest)
                {
                    TickVitals();
                    hoursRemaining--;
                    if (hoursRemaining < 1)
                        finished = true;
                }
                else if (currentRestMode == RestModes.FullRest)
                {
                    if (TickVitals())
                        finished = true;
                }
                else if (currentRestMode == RestModes.Loiter)
                {
                    hoursRemaining--;
                    if (hoursRemaining < 1)
                        finished = true;
                }
                // Check if rent expired
                if (CheckRent())
                    finished = true;
            }

            return finished;
        }

        private bool CheckRent()
        {
            if (remainingHoursRented > -1)
            {
                remainingHoursRented--;
                return (remainingHoursRented == 0);
            }
            return false;
        }

        void EndRest()
        {
            const int youWakeUpTextId = 353;
            const int enemiesNearby = 354;
            const int youAreHealedTextId = 350;
            const int finishedLoiteringTextId = 349;

            if (enemyBrokeRest)
            {
                DaggerfallMessageBox mb = DaggerfallUI.MessageBox(enemiesNearby);
                mb.OnClose += RestFinishedPopup_OnClose;
            }
            else
            {
                if (remainingHoursRented == 0)
                {
                    DaggerfallMessageBox mb = DaggerfallUI.MessageBox(HardStrings.expiredRentedRoom);
                    mb.OnClose += RestFinishedPopup_OnClose;
                    currentRestMode = RestModes.Selection;
                    playerEntity.RemoveExpiredRentedRooms();
                }
                else if (currentRestMode == RestModes.TimedRest)
                {
                    DaggerfallMessageBox mb = DaggerfallUI.MessageBox(youWakeUpTextId);
                    mb.OnClose += RestFinishedPopup_OnClose;
                    currentRestMode = RestModes.Selection;
                }
                else if (currentRestMode == RestModes.FullRest)
                {
                    DaggerfallMessageBox mb = DaggerfallUI.MessageBox(youAreHealedTextId);
                    mb.OnClose += RestFinishedPopup_OnClose;
                    currentRestMode = RestModes.Selection;
                }
                else if (currentRestMode == RestModes.Loiter)
                {
                    DaggerfallMessageBox mb = DaggerfallUI.MessageBox(finishedLoiteringTextId);
                    mb.OnClose += RestFinishedPopup_OnClose;
                    currentRestMode = RestModes.Selection;
                }
            }
        }

        bool TickVitals()
        {
            int healthRecoveryRate = FormulaHelper.CalculateHealthRecoveryRate(playerEntity);
            int fatigueRecoveryRate = FormulaHelper.CalculateFatigueRecoveryRate(playerEntity.MaxFatigue);
            int spellPointRecoveryRate = FormulaHelper.CalculateSpellPointRecoveryRate(playerEntity.MaxMagicka);

            playerEntity.CurrentHealth += healthRecoveryRate;
            playerEntity.CurrentFatigue += fatigueRecoveryRate;
            playerEntity.CurrentMagicka += spellPointRecoveryRate;

            playerEntity.TallySkill((short)Skills.Medical, 1);

            return IsPlayerFullyHealed();
        }

        bool IsPlayerFullyHealed()
        {
            // Check if player fully healed
            // Will eventually need to tailor check for character
            // For example, sorcerers cannot recover magicka from resting
            if (playerEntity.CurrentHealth == playerEntity.MaxHealth &&
                playerEntity.CurrentFatigue == playerEntity.MaxFatigue &&
                playerEntity.CurrentMagicka == playerEntity.MaxMagicka)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check if player is allowed to rest at this location.
        /// </summary>
        bool CanRest()
        {
            remainingHoursRented = -1;
            allocatedBed = Vector3.zero;
            PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
            PlayerGPS playerGPS = GameManager.Instance.PlayerGPS;

            bool inTown = playerGPS.IsPlayerInTown(true);

            if (inTown && !playerEnterExit.IsPlayerInside)
            {
                CloseWindow();
                DaggerfallUI.MessageBox(cityCampingIllegal);
                return false;
            }
            else if ((inTown || !playerGPS.HasCurrentLocation) && playerEnterExit.IsPlayerInsideBuilding)
            {
                string sceneName = DaggerfallInterior.GetSceneName(playerGPS.CurrentLocation.MapTableData.MapId, playerEnterExit.BuildingDiscoveryData.buildingKey);
                if (SaveLoadManager.StateManager.ContainsPermanentScene(sceneName))
                {
                    // Can rest if it's an player owned ship/house.
                    int buildingKey = playerEnterExit.BuildingDiscoveryData.buildingKey;
                    if (playerEnterExit.BuildingType == DFLocation.BuildingTypes.Ship || DaggerfallBankManager.IsHouseOwned(buildingKey))
                       return true;

                    // Find room rental record and get remaining time..
                    int mapId = playerGPS.CurrentLocation.MapTableData.MapId;
                    RoomRental_v1 room = GameManager.Instance.PlayerEntity.GetRentedRoom(mapId, buildingKey);
                    remainingHoursRented = PlayerEntity.GetRemainingHours(room);
                    allocatedBed = room.allocatedBed;
                    if (remainingHoursRented > 0)
                        return true;
                }
                CloseWindow();
                DaggerfallUI.MessageBox(HardStrings.haveNotRentedRoom);
                return false;
            }
            return true;
        }

        void MoveToBed()
        {
            if (allocatedBed != Vector3.zero)
            {
                PlayerMotor playerMotor = GameManager.Instance.PlayerMotor;
                playerMotor.transform.position = allocatedBed;
                playerMotor.FixStanding();
            }
        }

        #endregion

        #region Event Handlers

        private void WhileButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (CanRest())
            {
                DaggerfallInputMessageBox mb = new DaggerfallInputMessageBox(uiManager, this);
                mb.SetTextBoxLabel(HardStrings.restHowManyHours);
                mb.TextPanelDistanceX = 9;
                mb.TextPanelDistanceY = 8;
                mb.TextBox.Text = "0";
                mb.TextBox.Numeric = true;
                mb.TextBox.MaxCharacters = 8;
                mb.TextBox.WidthOverride = 286;
                mb.OnGotUserInput += TimedRestPrompt_OnGotUserInput;
                mb.Show();
            }
        }

        private void HealedButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (CanRest())
            {
                waitTimer = Time.realtimeSinceStartup;
                currentRestMode = RestModes.FullRest;
                MoveToBed();
            }
        }

        private void LoiterButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            DaggerfallInputMessageBox mb = new DaggerfallInputMessageBox(uiManager, this);
            mb.SetTextBoxLabel(HardStrings.loiterHowManyHours);
            mb.TextPanelDistanceX = 5;
            mb.TextPanelDistanceY = 8;
            mb.TextBox.Text = "0";
            mb.TextBox.Numeric = true;
            mb.TextBox.MaxCharacters = 8;
            mb.TextBox.WidthOverride = 286;
            mb.OnGotUserInput += LoiterPrompt_OnGotUserInput;
            mb.Show();
        }

        private void StopButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            DaggerfallUI.Instance.PopToHUD();
        }

        private void RestFinishedPopup_OnClose()
        {
            DaggerfallUI.Instance.PopToHUD();
            playerEntity.RaiseSkills();
        }

        #endregion

        #region Rest Events

        private void TimedRestPrompt_OnGotUserInput(DaggerfallInputMessageBox sender, string input)
        {
            const int cannotRestMoreThan99HoursTextId = 26;

            // Validate input
            int time = 0;
            bool result = int.TryParse(input, out time);
            if (!result)
                return;

            // Validate range
            if (time < 0)
            {
                time = 0;
            }
            else if (time > 99)
            {
                DaggerfallUI.MessageBox(cannotRestMoreThan99HoursTextId);
                return;
            }

            hoursRemaining = time;
            waitTimer = Time.realtimeSinceStartup;
            currentRestMode = RestModes.TimedRest;
            MoveToBed();
        }

        private void LoiterPrompt_OnGotUserInput(DaggerfallInputMessageBox sender, string input)
        {
            const int cannotLoiterMoreThan3HoursTextId = 27;

            // Validate input
            int time = 0;
            bool result = int.TryParse(input, out time);
            if (!result)
                return;

            // Validate range
            if (time < 0)
            {
                time = 0;
            }
            else if (time > 3)
            {
                DaggerfallUI.MessageBox(cannotLoiterMoreThan3HoursTextId);
                return;
            }

            hoursRemaining = time;
            waitTimer = Time.realtimeSinceStartup;
            currentRestMode = RestModes.Loiter;
        }

        #endregion
    }
}