// Copyright 2012 Simon Booth
// All rights reserved
// http://machinewrapped.wordpress.com/stereogrammer/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using Engine;

namespace Stereogrammer
{
    /// <summary>
    /// Interaction logic for SaveDialog.xaml
    /// </summary>
    public partial class DialogGenerateStereogram : Window
    {
        public Options generateOptions = null;
        public List<BitmapType> depthmaps = null;
        public List<BitmapType> textures = null;

        public bool SaveStereogram = false;

        public DialogGenerateStereogram(Options options, List<BitmapType> depthmaps, List<BitmapType> textures,
            bool saveIsDefault)
        {
            InitializeComponent();
            generateOptions = options;
            this.depthmaps = depthmaps;
            this.textures = textures;
            DataContext = options;

            buttonSave.IsDefault = saveIsDefault;
            buttonOK.IsDefault = !buttonSave.IsDefault;
        }

        private void GenerateDialog_Loaded(object sender, RoutedEventArgs e)
        {
            comboDepthmap.ItemsSource = depthmaps;
            comboDepthmap.SelectedItem = generateOptions.DepthMap;
            comboTexture.ItemsSource = textures;
            comboTexture.SelectedItem = generateOptions.Texture;
        }

        private void GenerateDialog_Closing(object sender, CancelEventArgs e)
        {
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            SaveStereogram = true;
            generateOptions.DepthMap = (DepthMap) comboDepthmap.SelectedItem;
            generateOptions.Texture = (Texture) comboTexture.SelectedItem;
            Close();
        }

        private void buttonOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            SaveStereogram = false;
            generateOptions.DepthMap = (DepthMap) comboDepthmap.SelectedItem;
            generateOptions.Texture = (Texture) comboTexture.SelectedItem;
            Close();
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            SaveStereogram = false;
            Close();
        }

        // Preserve aspect ratio if the option is selected
        private void textHeight_LostFocus(object sender, RoutedEventArgs e)
        {
            if (generateOptions.PreserveAspectRatio)
            {
                var ratio = (double) generateOptions.DepthMap.PixelWidth / generateOptions.DepthMap.PixelHeight;
                var resolutionY = Convert.ToInt32(textHeight.Text);
                var resolutionX = (int) (ratio * resolutionY);
                textWidth.Text = resolutionX.ToString();
                generateOptions.ResolutionX = resolutionX;
            }
        }

        private void textWidth_LostFocus(object sender, RoutedEventArgs e)
        {
            if (generateOptions.PreserveAspectRatio)
            {
                var ratio = (double) generateOptions.DepthMap.PixelHeight / generateOptions.DepthMap.PixelWidth;
                var resolutionX = Convert.ToInt32(textWidth.Text);
                var resolutionY = (int) (ratio * resolutionX);
                textHeight.Text = resolutionY.ToString();
                generateOptions.ResolutionY = resolutionY;
            }
        }

        private void checkBoxPreserveAspect_Checked(object sender, RoutedEventArgs e)
        {
            if (generateOptions.PreserveAspectRatio)
            {
                var ratio = (double) generateOptions.DepthMap.PixelWidth / generateOptions.DepthMap.PixelHeight;
                generateOptions.ResolutionX = (int) (ratio * generateOptions.ResolutionY);
                textWidth.Text =
                    generateOptions.ResolutionX
                        .ToString(); // Shirley data-binding is supposed to make that happen automatically?
            }
        }
    }
}