// Project:         Daggerfall Tools For Unity
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
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallConnect.Utility;
using DaggerfallConnect.FallExe;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.Player;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;

namespace DaggerfallWorkshop.Game.UserInterface
{
    /// <summary>
    /// Implements character paper doll.
    /// </summary>
    public class PaperDoll : Panel
    {
        #region UI Rects

        const int paperDollWidth = 110;
        const int paperDollHeight = 184;
        const int waistHeight = 40;

        Rect backgroundSubRect = new Rect(8, 7, paperDollWidth, paperDollHeight);

        #endregion

        #region Fields

        static Color32 maskColor = new Color(255, 0, 200, 0);   // Special mask colour used on helmets, cloaks, etc.
        DFPosition paperDollOrigin = new DFPosition(200, 8);    // Used to translate hard-coded IMG file offsets back to origin

        bool showBackgroundLayer = true;
        bool showCharacterLayer = true;

        Color32[] paperDollColors;                  // Paper doll as shown to player
        byte[] paperDollIndices;                    // Paper doll selection indices

        Texture2D paperDollTexture;

        Panel backgroundPanel = new Panel();
        Panel characterPanel = new Panel();

        TextLabel[] armourLabels = new TextLabel[DaggerfallEntity.NumberBodyParts];
        Vector2[] armourLabelPos = new Vector2[] { new Vector2(70, 12), new Vector2(20, 38), new Vector2(86, 38), new Vector2(12, 58), new Vector2(6, 90), new Vector2(18, 120), new Vector2(22, 168) };

        string lastBackgroundName = string.Empty;

        #endregion

        #region Properties

        public static Color32 MaskColor
        {
            get { return maskColor; }
        }

        #endregion

        #region Constructors

        public PaperDoll()
        {
            // Create target arrays
            paperDollColors = new Color32[paperDollWidth * paperDollHeight];
            paperDollIndices = new byte[paperDollWidth * paperDollHeight];

            // Setup panels
            Size = new Vector2(paperDollWidth, paperDollHeight);
            characterPanel.Size = new Vector2(paperDollWidth, paperDollHeight);

            // Add panels
            Components.Add(backgroundPanel);
            Components.Add(characterPanel);

            // Set initial display flags
            backgroundPanel.Enabled = showBackgroundLayer;
            characterPanel.Enabled = showCharacterLayer;

            for (int bpIdx = 0; bpIdx < DaggerfallEntity.NumberBodyParts; bpIdx++)
            {
                armourLabels[bpIdx] = DaggerfallUI.AddDefaultShadowedTextLabel(armourLabelPos[bpIdx], characterPanel);
                armourLabels[bpIdx].Text = "0";
            }
        }

        #endregion

        #region Overrides

        public override void Update()
        {
            // Update display flags
            backgroundPanel.Enabled = showBackgroundLayer;
            characterPanel.Enabled = showCharacterLayer;

            base.Update();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Redraws paper doll image and selection mask.
        /// Call this after changing equipment, loading a new game, etc.
        /// Only call when required as constructing paper doll image is expensive.
        /// </summary>
        /// <param name="playerEntity"></param>
        public void Refresh(PlayerEntity playerEntity = null)
        {
            // Get current player entity if one not provided
            if (playerEntity == null)
                playerEntity = GameManager.Instance.PlayerEntity;

            // Update paper doll
            ClearPaperDoll();
            RefreshBackground(playerEntity);
            BlitCloakInterior(playerEntity);
            BlitBody(playerEntity);
            BlitItems(playerEntity);

            // Destroy old paper doll texture
            characterPanel.BackgroundTexture = null;
            GameObject.Destroy(paperDollTexture);
            paperDollTexture = null;

            // Update paper doll texture
            paperDollTexture = ImageReader.GetTexture(paperDollColors, paperDollWidth, paperDollHeight);
            characterPanel.BackgroundTexture = paperDollTexture;

            RefreshArmourValues(playerEntity);

            //// Create image from selection mask
            //DFPalette palette = new DFPalette();
            //byte value = 20;
            //for (int i = 0; i < 256; i++)
            //{
            //    palette.Set(i, value, value, value);
            //    value += 8;
            //}
            //DFBitmap bitmap = new DFBitmap(paperDollWidth, paperDollHeight);
            //bitmap.Palette = palette;
            //bitmap.Data = (byte[])paperDollIndices.Clone();
            //Color32[] testColors = bitmap.GetColor32(255);
            //string path = @"d:\test\blits\selection.png";
            //Texture2D texture = ImageProcessing.MakeTexture2D(ref testColors, paperDollWidth, paperDollHeight, TextureFormat.RGBA32, false);
            //ImageProcessing.SaveTextureAsPng(texture, path);
        }

        /// <summary>
        /// Gets equip index at position.
        /// </summary>
        /// <param name="x">X position to sample.</param>
        /// <param name="y">Y position to sample.</param>
        /// <returns>Equip index or 0xff if point empty.</returns>
        public byte GetEquipIndex(int x, int y)
        {
            // Must have array
            if (paperDollIndices == null || paperDollIndices.Length == 0)
                return 0xff;

            // Ensure inside paper doll area
            if (x < 0 || x >= paperDollWidth)
                return 0xff;
            if (y < 0 || y >= paperDollHeight)
                return 0xff;

            // Get target index - must invert Y
            int ypos = paperDollHeight - y - 1;
            byte result = paperDollIndices[ypos * paperDollWidth + x];

            return result;
        }

        #endregion

        #region Private Methods

        // Refresh armour value labels
        void RefreshArmourValues(PlayerEntity playerEntity)
        {
            for (int bpIdx = 0; bpIdx < DaggerfallEntity.NumberBodyParts; bpIdx++)
            {
                sbyte av = playerEntity.ArmorValues[bpIdx];
                int bpAv = (100 - av) / 5;
                armourLabels[bpIdx].Text = bpAv.ToString();
            }
        }

        // Clear paper doll colours and indices
        void ClearPaperDoll()
        {
            //imageCounter = 0;
            for (int i = 0; i < paperDollWidth * paperDollHeight; i++)
            {
                paperDollColors[i] = Color.clear;
                paperDollIndices[i] = 0xff;
            }
        }

        // Update player background panel
        void RefreshBackground(PlayerEntity entity)
        {
            if (lastBackgroundName != entity.RaceTemplate.PaperDollBackground)
            {
                Texture2D texture = null;

                // Import custom image
                // We assume the image provided is already wihout borders
                if (DaggerfallWorkshop.Utility.AssetInjection.TextureReplacement.CustomImageExist(entity.RaceTemplate.PaperDollBackground))
                {
                    texture = DaggerfallWorkshop.Utility.AssetInjection.TextureReplacement.LoadCustomImage(entity.RaceTemplate.PaperDollBackground);
                    backgroundPanel.Size = new Vector2(paperDollWidth, paperDollHeight);
                }
                // Use vanilla Daggerfall image and remove borders
                else
                {
                    ImageData data = ImageReader.GetImageData(entity.RaceTemplate.PaperDollBackground, 0, 0, false, false);
                    texture = ImageReader.GetSubTexture(data, backgroundSubRect);
                    backgroundPanel.Size = new Vector2(texture.width, texture.height);
                }

                backgroundPanel.BackgroundTexture = texture;
                lastBackgroundName = entity.RaceTemplate.PaperDollBackground;
            }
        }

        void BlitCloakInterior(PlayerEntity entity)
        {
            // Draw cloak2 interior - stops here if this cloak is drawn
            DaggerfallUnityItem cloak2 = entity.ItemEquipTable.GetItem(EquipSlots.Cloak2);
            if (cloak2 != null)
            {
                ImageData interior2 = DaggerfallUnity.Instance.ItemHelper.GetCloakInteriorImage(cloak2);
                BlitPaperDoll(interior2, (byte)cloak2.EquipSlot);
                return;
            }

            // Draw cloak1 interior
            DaggerfallUnityItem cloak1 = entity.ItemEquipTable.GetItem(EquipSlots.Cloak1);
            if (cloak1 != null)
            {
                ImageData interior1 = DaggerfallUnity.Instance.ItemHelper.GetCloakInteriorImage(cloak1);
                BlitPaperDoll(interior1, (byte)cloak1.EquipSlot);
            }
        }

        // Copy body parts to target
        void BlitBody(PlayerEntity entity)
        {
            // Get gender-based body parts
            ImageData nudeBody = new ImageData();
            ImageData clothedBody = new ImageData();
            ImageData head = new ImageData();
            if (entity.Gender == Genders.Male)
            {
                nudeBody = ImageReader.GetImageData(entity.RaceTemplate.PaperDollBodyMaleUnclothed, 0, 0, true);
                clothedBody = ImageReader.GetImageData(entity.RaceTemplate.PaperDollBodyMaleClothed, 0, 0, true);
                head = ImageReader.GetImageData(entity.RaceTemplate.PaperDollHeadsMale, entity.FaceIndex, 0, true);
            }
            else if (entity.Gender == Genders.Female)
            {
                nudeBody = ImageReader.GetImageData(entity.RaceTemplate.PaperDollBodyFemaleUnclothed, 0, 0, true);
                clothedBody = ImageReader.GetImageData(entity.RaceTemplate.PaperDollBodyFemaleClothed, 0, 0, true);
                head = ImageReader.GetImageData(entity.RaceTemplate.PaperDollHeadsFemale, entity.FaceIndex, 0, true);
            }
            else
            {
                return;
            }

            // Draw standard body
            BlitPaperDoll(nudeBody);

            // Censor nudity if this setting enabled by using welded-on clothes.
            // But only if censored part of body is actually unclothed.
            // Otherwise welded-on clothes can be visible around equipped clothes.
            // This involves a special blit to draw top and bottom halves independently.
            if (!DaggerfallUnity.Settings.PlayerNudity)
            {
                if (!entity.ItemEquipTable.IsUpperClothed())
                    BlitUpperBody(clothedBody);

                if (!entity.ItemEquipTable.IsLowerClothed())
                    BlitLowerBody(clothedBody);
            }

            // Blit head
            BlitPaperDoll(head);
        }

        // Blit items
        void BlitItems(PlayerEntity entity)
        {
            // Create list of all equipped items
            List<DaggerfallUnityItem> equippedItems = new List<DaggerfallUnityItem>();

            // Find equipped slots, skip empty slots
            foreach (var item in entity.ItemEquipTable.EquipTable)
            {
                if (item != null)
                {
                    equippedItems.Add(item);
                }
            }

            // Sort equipped items by draw order
            List<DaggerfallUnityItem> orderedItems = equippedItems.OrderBy(o => o.drawOrder).ToList();

            // Blit item images
            foreach(var item in orderedItems)
            {
                BlitItem(item);
            }
        }

        // Blits a normal item
        void BlitItem(DaggerfallUnityItem item)
        {
            ImageData source = DaggerfallUnity.Instance.ItemHelper.GetItemImage(item, maskColor, true);
            BlitPaperDoll(source, (byte)item.EquipSlot);
        }

        #endregion

        #region Custom Image Processing

        // Blit source ImageData onto paper doll
        void BlitPaperDoll(ImageData source, byte index = 0xff)
        {
            Color32[] colors = ImageReader.GetColors(source);

            BlitImage(
                ref colors,
                new Vector2(source.width, source.height),
                new Vector2(paperDollWidth, paperDollHeight),
                new Vector2(source.offset.X, source.offset.Y),
                maskColor,
                index);
        }

        // Special-purpose blit function to build paper doll image in 32-bit RGBA.
        public void BlitImage(
            ref Color32[] source,
            Vector2 sourceSize,
            Vector2 targetSize,
            Vector2 targetPosition,
            Color32 maskColor,
            byte index = 0xff)
        {
            // Calculate image offsets
            int xOffset = (int)targetPosition.x - paperDollOrigin.X;
            int yOffset = (int)((targetSize.y - sourceSize.y) - (targetPosition.y - paperDollOrigin.Y));

            // Copy image data
            for (int y = 0; y < sourceSize.y; y++)
            {
                for (int x = 0; x < sourceSize.x; x++)
                {
                    // Get source colour
                    Color col = source[y * (int)sourceSize.x + x];
                    if (col == Color.clear)
                        continue;

                    // Handle item masking
                    bool isMask = false;
                    if (col == maskColor)
                    {
                        col = Color.clear;
                        isMask = true;
                    }

                    // Get target offsets and ensure inside paper doll area
                    int targetX = xOffset + x;
                    int targetY = yOffset + y;
                    if (targetX < 0 || targetX >= paperDollWidth) continue;
                    if (targetY < 0 || targetY >= paperDollHeight) continue;

                    // Write colour to target array
                    int targetOffset = targetY * paperDollWidth + targetX;
                    paperDollColors[targetOffset] = col;

                    // Write index to target array
                    if (isMask)
                        paperDollIndices[targetOffset] = 0xff;
                    else
                        paperDollIndices[targetOffset] = index;
                }
            }

            //// Generate a test texture
            //string path = @"d:\test\blits\" + imageCounter++;
            //Texture2D texture = ImageProcessing.MakeTexture2D(ref paperDollColors, paperDollWidth, paperDollHeight, TextureFormat.RGBA32, false);
            //ImageProcessing.SaveTextureAsPng(texture, path);
        }

        #endregion

        #region ChildGuard Image Processing

        // Special blit for upper half of player body
        void BlitUpperBody(ImageData body)
        {
            // Get body above waist
            Color32[] colors = ImageReader.GetColors(body);
            Rect rect = new Rect(0, body.height - waistHeight, body.width, waistHeight);
            Color32[] newColors = ImageReader.GetSubColors(colors, rect, (int)rect.width, (int)rect.height);

            BlitImage(
                ref newColors,
                new Vector2(rect.width, rect.height),
                new Vector2(paperDollWidth, paperDollHeight),
                new Vector2(body.offset.X, body.offset.Y),
                maskColor);
        }

        // Special blit for lower half of player body
        void BlitLowerBody(ImageData body)
        {
            Color32[] colors = ImageReader.GetColors(body);
            Rect rect = new Rect(0, 0, body.width, body.height - waistHeight);
            Color32[] newColors = ImageReader.GetSubColors(colors, rect, (int)rect.width, (int)rect.height);

            BlitImage(
                ref newColors,
                new Vector2(rect.width, rect.height),
                new Vector2(paperDollWidth, paperDollHeight),
                new Vector2(body.offset.X, body.offset.Y + waistHeight),
                maskColor);
        }

        #endregion
    }
}