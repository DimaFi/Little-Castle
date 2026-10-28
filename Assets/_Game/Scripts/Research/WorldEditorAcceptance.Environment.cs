using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Acceptance
{
    public sealed partial class WorldEditorAcceptance
    {
        IEnumerator EnvironmentChecks()
        {
            string original=painter.ExportJson();
            var session=painter.Session;var tools=painter.Tools;
            try
            {
                QualitySettings.renderPipeline=ao;Pose(35);SetHour(12);
                for(int i=0;i<30;i++)yield return null;
                yield return Capture("environment-summer-before");
                tools.Select("environment.biome");tools.context.settings.radius=24;
                tools.context.settings.strength=1;tools.context.settings.softness=.55f;tools.context.environmentThemeId=3;
                var timer=Stopwatch.StartNew();
                tools.Begin(new MapToolPointer(default,true,new Vector2(-4,5)),0);tools.Complete();timer.Stop();
                Require(session.EnvironmentThemes.Sample(3,-4,5)>.9f,"Winter brush did not change the live map.");
                for(int i=0;i<300&&painter.EnvironmentView.PendingUploadTiles>0;i++)yield return null;
                // One frame starts the queue when the first notifications arrived before Tick.
                for(int i=0;i<12;i++)yield return null;
                foreach(float distance in new[]{11f,35f,115f})
                {Pose(distance);for(int i=0;i<24;i++)yield return null;yield return Capture("environment-winter-day-d"+F(distance));}
                SetHour(1);Pose(35);for(int i=0;i<24;i++)yield return null;yield return Capture("environment-winter-night");
                string winter=session.ExportJson();session.Undo();Require(session.EnvironmentThemes.Sample(3,-4,5)<.01f,"Winter undo failed.");
                session.Redo();Require(session.ExportJson()==winter,"Winter redo failed.");
                var settings=session.EnvironmentSettings;settings.simulateWeather=true;settings.snowfallIntensity=.6f;session.SetEnvironmentSettings(settings);
                SetHour(12);Pose(35);for(int i=0;i<40;i++)yield return null;yield return Capture("environment-winter-snowfall");
                Require(painter.ShoreDecorationView!=null,"Automatic shore presenter was not connected.");
                File.WriteAllText(Path.Combine(output,"environment-checks.txt"),
                    "PASS live shared biome brush, Undo/Redo, day/night/near/middle/far renders, local snowfall.\n"+
                    "Brush+commit milliseconds="+F(timer.Elapsed.TotalMilliseconds)+"\n"+
                    "Resident shore stones="+painter.ShoreDecorationView.ResidentInstances+"\n"+
                    "Resident environment bounds="+painter.EnvironmentView.ResidentBounds+"\n"+
                    "All modifications are isolated Player memory; authored assets/save path were not written.\n");
            }
            finally{painter.LoadJson(original);}
        }
    }
}
