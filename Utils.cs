using CitizenFX.Core;
using FivePD.API.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CitizenFX.Core.Native;
using CitizenFX.Core.NaturalMotion;
using FivePD.API;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Drawing;

namespace Kilo.Commons.Utils
{
    public class Utils
    {
        public static JObject config;
        public static Dictionary<int, Marker> markers = new Dictionary<int, Marker>();

        public class Waypoint
        {
            public Vector3 Position
            {
                get { return _position; }
            }

            public Entity Target
            {
                get { return _entity; }
            }

            public float Distance
            {
                get { return _distance; }
            }

            private bool _arrived = false;
            private float _distance;
            private float _bufferDistance;
            private Vector3 _position;
            private Entity _entity;
            private int _refreshInterval;
            private Marker _visualMarker;
            private float _runDistance = 10f;
            public float RunDistance
            {
                get { return _runDistance; }
            }

            public float DrivingSpeed
            {
                get { return _drivingSpeed; }
            }
            private float _drivingSpeed = 20f;

            private int _timeout;
            
            public Waypoint(Vector3 position, Entity entityToTrack, int timeout = 100, float bufferDistance = 0.1f,
                int refreshInterval = 100)
            {
                _position = position;
                _entity = entityToTrack;
                _bufferDistance = bufferDistance;
                _refreshInterval = refreshInterval;
                _timeout = timeout;
                UpdateData();
            }

            private GoToType CalculateGoToType()
            {
                GoToType goToType = GoToType.Run;
                if (_distance < RunDistance)
                {
                    goToType = GoToType.Walk;
                }
                return goToType;
            }

            public async Task Start(float drivingSpeed = -1f)
            {
                await BaseScript.Delay(_timeout);
                if (!Target.Model.IsValid) return;
                if (Target.Model.IsPed)
                {
                    var ped = (Ped)Target;
                    KeepTaskGoToForPed(ped, Position, _bufferDistance, CalculateGoToType());
                } else if (Target.Model.IsVehicle)
                {
                    if (drivingSpeed != -1f)
                        _drivingSpeed = drivingSpeed;
                    Drive();
                }
            }

            private void Drive()
            {
                var veh = (Vehicle)Target;
                if (veh.Driver == null) throw new Exception("Vehicle needs a driver in order to start drive!");
                var driver = veh.Driver;
                driver.Task.DriveTo(veh, Position, _bufferDistance, _drivingSpeed);
            }

            public void SetDrivingSpeed(float speed)
            {
                _drivingSpeed = speed;
                Drive();
            }
            
            public void SetRunDistance(float distance)
            {
                _runDistance = distance;
            }

            private async Task UpdateData()
            {
                while (!_arrived)
                {
                    _distance = Target.Position.DistanceTo(Position);
                    _arrived = _distance <= _bufferDistance;
                    await BaseScript.Delay(_refreshInterval);
                }
            }

            public void Mark(MarkerType markerType)
            {
                if (_visualMarker != null)
                    throw new Exception("Marker already exists!");

                _visualMarker = new Marker(markerType, MarkerAttachTo.Position, Position);
                _visualMarker.SetVisiblility(true);
            }

            public void Unmark()
            {
                if (_visualMarker == null)
                    throw new Exception("Marker does not exist!");
                _visualMarker.Dispose();
            }

            public async Task Wait()
            {
                while (!_arrived)
                {
                    await BaseScript.Delay(_refreshInterval);
                }

                if (Target.Model.IsVehicle)
                {
                    var veh = (Vehicle)Target;
                    var ped = veh.Driver;
                    ped.Task.ClearAll();
                }
                else
                {
                    var ped = (Ped)Target;
                    ped.Task.ClearAll();
                }
            }
        }

        public enum MarkerAttachTo
        {
            Entity,
            Position
        }

        public class Marker
        {
            public int Handle;

            public bool Enabled
            {
                get { return _enabled; }
            }

            private bool _enabled = true;
            private bool destroyed = false;
            private MarkerType _markerType;
            private Vector3 _pos;

            public Marker(MarkerType markerType, MarkerAttachTo markerAttachTo, Vector3 pos, Entity entity = null)
            {
                _markerType = markerType;
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                _pos = pos;
                if (markerAttachTo == MarkerAttachTo.Entity)
                {
                    if (entity == null) throw new Exception("You need to provide a valid entity to attach to!");
                    AttachPositionToEntity(entity, pos);
                }

                SetHandle();
                Create();
            }

            private async void AttachPositionToEntity(Entity entity, Vector3 offsetPos)
            {
                while (!destroyed)
                {
                    Vector3 newPos = entity.Position.ApplyOffset(offsetPos);
                    _pos = newPos;
                    await BaseScript.Delay(0);
                }
            }

            public void SetVisiblility(bool state)
            {
                _enabled = state;
            }

            private async void Create()
            {
                _enabled = true;
                while (!destroyed)
                {
                    if (Enabled)
                    {
                        World.DrawMarker(_markerType, _pos, Vector3.Zero, Vector3.Zero, Vector3.One, Color.Aqua);
                    }

                    await BaseScript.Delay(0);
                }
            }

            public void Dispose()
            {
                destroyed = true;
                _enabled = false;
            }

            private async void SetHandle()
            {
                int _handle = new Random().Next();
                while (markers.ContainsKey(_handle) && !destroyed)
                {
                    _handle = new Random().Next();
                    await BaseScript.Delay(0);
                }

                Handle = _handle;
            }
        }

        public static async Task<string> DoOnScreenKeyboard()
        {
            string text = "";

            API.DisplayOnscreenKeyboard(0, "FMMC_KEY_TIP8", "", "", "", "", "", 60);
            while (API.UpdateOnscreenKeyboard() == 0)
            {
                API.DisableAllControlActions(0);
                await BaseScript.Delay(0);
            }

            if (API.GetOnscreenKeyboardResult() == null) return text;
            text = API.GetOnscreenKeyboardResult();
            return text;
        }

        public static void ShowNetworkedNotification(string text, string sender = "~f~Dispatch",
            string subject = "~m~ Callout Update", string txdict = "CHAR_CALL911", string txname = "CHAR_CALL911",
            int iconType = 4, int backgroundColor = -1, bool flash = false, bool isImportant = false,
            bool saveToBrief = false)
        {
            API.BeginTextCommandThefeedPost("STRING");
            API.AddTextComponentSubstringPlayerName(text);
            if (backgroundColor > -1)
                API.ThefeedNextPostBackgroundColor(
                    backgroundColor); // https://docs.fivem.net/docs/game-references/hud-colors/
            API.EndTextCommandThefeedPostMessagetext(txdict, txname, flash, iconType, sender, subject);
            API.EndTextCommandThefeedPostTicker(isImportant, saveToBrief);
        }

        public static void ShowNotification(string text)
        {
            API.SetTextComponentFormat("STRING");
            API.AddTextComponentString(text);
            API.DisplayHelpTextFromStringLabel(0, false, true, -1);
        }

        public static List<string> animDictsLoaded = new List<string>();
        public static List<string> animSetsLoaded = new List<string>();

        public static Prop GetClosestObjectToCoords(float x, float y, float z, float buffer = 0.2f,
            string modelName = null)
        {
            Prop[] allProps = World.GetAllProps();
            Prop closestProp = null;
            foreach (var p in allProps)
            {
                if (p == null) continue;
                if (modelName != null && p.Model.Hash != API.GetHashKey(modelName)) continue;
                if (p.Position.DistanceTo(new Vector3(x, y, z)) < buffer) continue;
                if (closestProp == null)
                    closestProp = p;
                if (p.Position.DistanceTo(new(x, y, z)) < closestProp.Position.DistanceTo(new(x, y, z)))
                    closestProp = p;
            }

            return closestProp;
        }

        public static Vector2 decisionUIPos = new Vector2(0.8f, 0.8f);
        public static List<string> displaying2DText = new List<string>();
        public static bool displayingDecision = false;
        public static bool selectEnabled = false;
        public static int currentselected = -1;

        public static void ClearDecisions()
        {
            displaying2DText.Clear();
        }

        public class DecisionInteraction
        {
            public bool IsActive = true;
            private Dictionary<int, Action> connected = new Dictionary<int, Action>();

            public DecisionInteraction(string[] choices)
            {
                if (!IsActive) return;
                ShowInteractionDecision(choices);
            }

            public void Show()
            {
                displayingDecision = true;
                List<string> lines = UpdateChoicesIndices(displaying2DText);
                displaying2DText = lines;
                HandleInteractButton();
            }

            public void Connect(int index, Action function)
            {
                if (!IsActive) return;

                if (!connected.ContainsKey(index))
                {
                    connected.Add(index, function);
                }
            }


            private async Task ShowInteractionDecision(string[] choices)
            {
                if (!IsActive) return;
                ClearDecisions();
                List<string> lines = ConvertChoicesIntoLines(choices);
                foreach (var line in lines)
                {
                    displaying2DText.Add(line);
                    Draw2DHandler(line);
                }

                displayingDecision = true;
                HandleInteractButton();
            }

            private async Task HandleInteractButton()
            {
                while (displayingDecision)
                {
                    if (!IsActive) return;
                    if (Game.IsControlJustReleased(0, Control.MpTextChatTeam))
                    {
                        if (!selectEnabled)
                            currentselected = 0;
                        selectEnabled = !selectEnabled;
                        API.SetNuiFocusKeepInput(true);
                        API.SetNuiFocus(selectEnabled, false);
                        await BaseScript.Delay(100);
                    }

                    if (Game.IsControlJustReleased(0, Control.FrontendDown))
                    {
                        if (selectEnabled)
                        {
                            if ((displaying2DText.Count - 2) >= currentselected)
                                currentselected++;
                            else
                                currentselected = 0;
                            await BaseScript.Delay(100);
                        }
                    }

                    if (Game.IsControlJustReleased(0, Control.FrontendUp))
                    {
                        if (selectEnabled)
                        {
                            if (currentselected <= 0)
                                currentselected = displaying2DText.Count - 1;
                            else
                                currentselected--;
                        }

                        await BaseScript.Delay(100);
                    }

                    if (Game.IsControlJustReleased(0, Control.FrontendEndscreenAccept))
                    {
                        if (selectEnabled)
                        {
                            displayingDecision = false;
                            selectEnabled = false;
                            API.SetNuiFocus(selectEnabled, false);
                            connected[currentselected]();
                            //Debug.WriteLine("Should be running");
                        }
                    }

                    await BaseScript.Delay(0);
                }
            }

            public void Dispose()
            {
                IsActive = false;
            }
        }


        public static async Task Remove2D(string text)
        {
            displaying2DText.Remove(text);
        }

        public static async Task Draw2DHandler(string text)
        {
            Vector2 pos = decisionUIPos;
            int index = displaying2DText.IndexOf(text);
            if (text == "") return;
            foreach (var s in displaying2DText)
            {
                int i = displaying2DText.IndexOf(s);
                if (i < index)
                    pos += new Vector2(0f, 0.03f);
            }

            Draw2D(text, pos);
        }

        public static async Task Draw2D(string text, Vector2 pos)
        {
            while (displaying2DText.Contains(text))
            {
                if (displayingDecision)
                {
                    API.SetTextScale(0f, 0.5f);
                    API.SetTextFont(0);
                    API.SetTextProportional(true);
                    if (selectEnabled && currentselected == displaying2DText.IndexOf(text))
                        API.SetTextColour(255, 255, 255, 255);
                    else
                        API.SetTextColour(255, 255, 255, 100);

                    API.SetTextDropshadow(0, 0, 0, 0, 255);
                    API.SetTextEdge(2, 0, 0, 0, 150);
                    API.SetTextDropShadow();
                    API.SetTextOutline();
                    API.SetTextEntry("STRING");
                    API.SetTextCentre(true);
                    API.AddTextComponentString(text);
                    API.DrawText(pos.X, pos.Y);
                }

                await BaseScript.Delay(0);
            }
        }

        public static List<string> UpdateChoicesIndices(List<string> choices)
        {
            List<string> newchoices = choices;
            foreach (var choice in newchoices.ToArray())
            {
                int index = displaying2DText.IndexOf(choice);
                string newchoice = choice.Replace("" + (index + 1).ToString() + ")", "").Trim();
                newchoices[index] = newchoices.IndexOf(choice) + ") " + newchoice;
            }

            return newchoices;
        }

        public static List<string> ConvertChoicesIntoLines(string[] choices)
        {
            List<string> lines = new List<string>();
            int i = 0;
            foreach (var choice in choices)
            {
                i++;
                lines.Add("" + i + ") " + choice);
            }

            return lines;
        }

        public static async Task<bool> WaitUntilKeypressed(Control key, int timeoutAfterDuration = -1)
        {
            bool stillWorking = true;
            bool pressed = false;
            if (timeoutAfterDuration > -1)
            {
                var wait = new Action(async () =>
                {
                    await BaseScript.Delay(timeoutAfterDuration);
                    stillWorking = false;
                });
                wait();
            }

            while (stillWorking)
            {
                if (Game.IsControlJustReleased(0, key))
                {
                    pressed = true;
                    return pressed;
                }

                await BaseScript.Delay(0);
            }

            return pressed;
        }

        public static List<string> Text3DInProgress = new List<string>();

        public static void ImmediatelyStop3DText()
        {
            Text3DInProgress.Clear();
        }

        public static async void Draw3DText(Vector3 pos, string text, float scaleFactor = 0.5f,
            int duration = 5000, int red = 255, int green = 255, int blue = 255, int opacity = 150,
            Entity attachTo = null)
        {
            if (attachTo == null)
            {
                Text3DInProgress.Add(text);
                Draw3DTextHandler(pos, scaleFactor, text, duration, red, green, blue, opacity);
            }
            else
            {
                // Pos is offset
                Text3DInProgress.Add(text);
                Draw3DTextDrawerOnEntity(attachTo, pos, scaleFactor, text, red, green, blue, opacity);
                await BaseScript.Delay(duration);
                if (Text3DInProgress.Contains(text))
                    Text3DInProgress.Remove(text);
            }
        }

        public static async Task Draw3DTextHandler(Vector3 pos, float scaleFactor, string text, int duration,
            int red, int green, int blue, int opacity)
        {
            Draw3DTextDrawer(pos, scaleFactor, text, red, green, blue, opacity);
            await BaseScript.Delay(duration);
            if (Text3DInProgress.Contains(text))
                Text3DInProgress.Remove(text);
        }

        public static async Task ShowDialogCountdown(string text, int duration = 10000)
        {
            string countdownReplace = "[Countdown]";
            int seconds = duration / 1000;
            bool stillWorking = true;
            var wait = new Action(async () =>
            {
                await BaseScript.Delay(duration);
                stillWorking = false;
            });
            wait();
            var wait2 = new Action(async () =>
            {
                await Utils.WaitUntilKeypressed(Control.MpTextChatTeam, 20000);
                stillWorking = false;
            });
            wait2();
            while (stillWorking)
            {
                string newText = text.Replace(countdownReplace, "" + seconds + " seconds left");
                API.BeginTextCommandPrint("STRING");
                API.AddTextComponentString(newText);
                API.EndTextCommandPrint(1000, true);
                seconds -= 1;
                await BaseScript.Delay(1000);
            }
        }

        public static async Task ShowDialog(string text, int duration = 10000, bool showImmediately = false)
        {
            API.BeginTextCommandPrint("STRING");
            API.AddTextComponentString(text);
            API.EndTextCommandPrint(duration, showImmediately);
            await BaseScript.Delay(duration);
        }

        public static async Task SubtitleChat(Entity entity, string chat, int red = 255, int green = 255,
            int blue = 255,
            int opacity = 255)
        {
            int time = chat.Length * 150;
            Utils.Draw3DText(new Vector3(0f, 0f, 1f), chat, 0.5f,
                time,
                red, green, blue, opacity, entity);
            await BaseScript.Delay(time);
        }

        public static void Draw3DTextDrawNonLoop(Vector3 pos, float scaleFactor, string text, int red, int green,
            int blue, int opacity)
        {
            float screenY = 0f;
            float screenX = 0f;
            bool result = API.World3dToScreen2d(pos.X, pos.Y, pos.Z, ref screenX, ref screenY);
            Vector3 p = API.GetGameplayCamCoords();
            float dist = World.GetDistance(p, pos);
            float scale = (1 / dist) * 2;
            float fov = (1 / API.GetGameplayCamFov()) * 100;
            scale = scale * fov * scaleFactor;
            if (!result) return;
            API.SetTextScale(0f, scale);
            API.SetTextFont(0);
            API.SetTextProportional(true);
            API.SetTextColour(red, green, blue, opacity);
            API.SetTextDropshadow(0, 0, 0, 0, 255);
            API.SetTextEdge(2, 0, 0, 0, 150);
            API.SetTextDropShadow();
            API.SetTextOutline();
            API.SetTextEntry("STRING");
            API.SetTextCentre(true);
            API.AddTextComponentString(text);
            API.DrawText(screenX, screenY);
        }

        public static async Task Draw3DTextDrawerOnEntity(Entity ent, Vector3 offset, float scaleFactor, string text,
            int red, int green,
            int blue, int opacity)
        {
            while (Text3DInProgress.Contains(text))
            {
                Vector3 pos = API.GetOffsetFromEntityInWorldCoords(ent.Handle, offset.X, offset.Y, offset.Z);
                Draw3DTextDrawNonLoop(pos, scaleFactor, text, red, green, blue, opacity);
                await BaseScript.Delay(0);
            }
        }

        public static async Task Draw3DTextDrawer(Vector3 pos, float scaleFactor, string text, int red, int green,
            int blue, int opacity)
        {
            while (Text3DInProgress.Contains(text))
            {
                Draw3DTextDrawNonLoop(pos, scaleFactor, text, red, green, blue, opacity);
                await BaseScript.Delay(0);
            }
        }

        public static async Task CaptureEntity(Entity ent)
        {
            API.NetworkRequestControlOfEntity(ent.Handle);
            ent.IsPersistent = true;
            if (ent.Model.IsPed)
            {
                KeepTask((Ped)ent);
            }
        }

        public static bool CanEntitySeeEntity(Entity ent1, Entity ent2)
        {
            return API.HasEntityClearLosToEntityInFront(ent1.Handle, ent2.Handle);
        }


        public static async Task WaitUntilPedCanSeePed(Ped ped1, Ped ped2, int bufferms = 1000)
        {
            while (!API.HasEntityClearLosToEntityInFront(ped1.Handle, ped2.Handle))
                await BaseScript.Delay(bufferms);
        }

        public static async Task WaitUntilVehicleEngine(Vehicle veh, bool state = false)
        {
            while (veh.IsEngineRunning != state)
                await BaseScript.Delay(100);
        }

        public static Ped GetClosestPed(Vector3 pos, float maxDistance = 20f, bool ignoreVehicles = false,
            bool findAlive = true, bool findPlayers = false)
        {
            Ped[] allPeds = World.GetAllPeds();
            Ped closest = null;
            foreach (var p in allPeds)
            {
                if (p == null || !p.Exists()) continue;
                if (p.Position.DistanceTo(pos) > maxDistance) continue;
                if (ignoreVehicles && p.IsInVehicle()) continue;
                if (p.NetworkId == Game.PlayerPed.NetworkId) continue;
                if (!findAlive != p.IsDead) continue;
                if (findPlayers != p.IsPlayer) continue;
                if (closest == null)
                    closest = p;
                if (p.Position.DistanceTo(pos) < closest.Position.DistanceTo(pos))
                    closest = p;
            }

            return closest;
        }

        public static async Task PedTaskLaptopHackAnimation(Ped ped, int duration = 10000)
        {
            Ped hacker = ped;
            /*hacker.Task.RunTo(runToPos);
            KeepTaskGoToForPed(hacker, runToPos, 1f);
            await WaitUntilEntityIsAtPos(hacker, runToPos, 1f);
            hacker.Task.AchieveHeading(249.7f);
            await BaseScript.Delay(750);*/

            int bagscene = Function.Call<int>(Hash.NETWORK_CREATE_SYNCHRONISED_SCENE, hacker.Position.X - 0.5f,
                hacker.Position.Y, hacker.Position.Z + 0.4f, hacker.Rotation.X, hacker.Rotation.Y, hacker.Rotation.Z, 2,
                false, false, 1065353216, 0, 1.3);
            int bag = Function.Call<int>(Hash.CREATE_OBJECT,
                Function.Call<uint>(Hash.GET_HASH_KEY, "hei_p_m_bag_var22_arm_s"), hacker.Position.X, hacker.Position.Y,
                hacker.Position.Z + 0.2, true, true, false);
            Function.Call(Hash.SET_ENTITY_COLLISION, bag, false, true);
            Entity bagentity = Entity.FromHandle(bag);

            int laptop = Function.Call<int>(Hash.CREATE_OBJECT,
                Function.Call<uint>(Hash.GET_HASH_KEY, "hei_prop_hst_laptop"), hacker.Position.X, hacker.Position.Y,
                hacker.Position.Z + 0.2, true, true, true);
            Entity thermiteEntity = Entity.FromHandle(laptop);
            Function.Call(Hash.SET_ENTITY_COLLISION, laptop, false, true);

            Function.Call(Hash.NETWORK_ADD_PED_TO_SYNCHRONISED_SCENE, hacker, bagscene, "anim@heists@ornate_bank@hack",
                "hack_enter", 1.5, -4.0, 1, 16, 1148846080, 0);
            Function.Call(Hash.NETWORK_ADD_ENTITY_TO_SYNCHRONISED_SCENE, bag, bagscene, "anim@heists@ornate_bank@hack",
                "hack_enter_bag", 4.0, -8.0, 1);
            Function.Call(Hash.NETWORK_ADD_ENTITY_TO_SYNCHRONISED_SCENE, laptop, bagscene,
                "anim@heists@ornate_bank@hack", "hack_enter_laptop", 4.0, -8.0, 1);
            Function.Call(Hash.SET_PED_COMPONENT_VARIATION, hacker, 5, 0, 0, 0);
            Function.Call(Hash.NETWORK_START_SYNCHRONISED_SCENE, bagscene);
            await BaseScript.Delay(6000);
            Function.Call(Hash.NETWORK_STOP_SYNCHRONISED_SCENE, bagscene);

            int hackscene = Function.Call<int>(Hash.NETWORK_CREATE_SYNCHRONISED_SCENE, hacker.Position.X - 0.5f,
                hacker.Position.Y, hacker.Position.Z + 0.4f, hacker.Rotation.X, hacker.Rotation.Y, hacker.Rotation.Z, 2,
                false, true, 1065353216, 0, 1);
            Function.Call(Hash.NETWORK_ADD_PED_TO_SYNCHRONISED_SCENE, hacker, hackscene, "anim@heists@ornate_bank@hack",
                "hack_loop", 0, 0, 1, 16, 1148846080, 0);
            Function.Call(Hash.NETWORK_ADD_ENTITY_TO_SYNCHRONISED_SCENE, bag, hackscene, "anim@heists@ornate_bank@hack",
                "hack_loop_bag", 4.0, -8.0, 1);
            Function.Call(Hash.NETWORK_ADD_ENTITY_TO_SYNCHRONISED_SCENE, laptop, hackscene,
                "anim@heists@ornate_bank@hack", "hack_loop_laptop", 1.0, -0.0, 1);
            Function.Call(Hash.NETWORK_START_SYNCHRONISED_SCENE, hackscene);
            await BaseScript.Delay(duration);
            Function.Call(Hash.NETWORK_STOP_SYNCHRONISED_SCENE, hackscene);

            int hackexit = Function.Call<int>(Hash.NETWORK_CREATE_SYNCHRONISED_SCENE, hacker.Position.X - 0.5f,
                hacker.Position.Y, hacker.Position.Z + 0.4f, hacker.Rotation.X, hacker.Rotation.Y, hacker.Rotation.Z, 2,
                false, false, 1065353216, -1, 1.3);
            Function.Call(Hash.NETWORK_ADD_PED_TO_SYNCHRONISED_SCENE, hacker, hackexit, "anim@heists@ornate_bank@hack",
                "hack_exit", 0, 0, -1, 16, 1148846080, 0);
            Function.Call(Hash.NETWORK_ADD_ENTITY_TO_SYNCHRONISED_SCENE, bag, hackexit, "anim@heists@ornate_bank@hack",
                "hack_exit_bag", 4.0, -8.0, 1);
            Function.Call(Hash.NETWORK_ADD_ENTITY_TO_SYNCHRONISED_SCENE, laptop, hackexit,
                "anim@heists@ornate_bank@hack", "hack_exit_laptop", 4.0, -8.0, 1);
            Function.Call(Hash.NETWORK_START_SYNCHRONISED_SCENE, hackexit);
            await BaseScript.Delay(6000);
            Function.Call(Hash.NETWORK_STOP_SYNCHRONISED_SCENE, hackexit);
            bagentity.Delete();
            Entity laptope = Entity.FromHandle(laptop);
            laptope.Delete();
        }

        public static void CalloutError(Exception err, Callout callout)
        {
            try
            {
                //Debug.WriteLine("^3=================^Kilo's CPR Plugin [FivePD]^3=================");
                //Debug.WriteLine("^8ERROR DETECTED^7: " + err.Message);
                //Debug.WriteLine("^3=================^2Kilo' CPR Plugin [FivePD]^3=================");
                //Debug.WriteLine("^5Callout Ended");
                try
                {
                    if ((bool)config["AnonymousErrorReporting"])
                    {
                        //BaseScript.TriggerServerEvent("247Robbery::ErrorToWebhook",
                        //  "Error in 247Robbery Callout: \n" + err.ToString());
                    }
                }
                catch (Exception err2)
                {
                    // Do nothing
                }

                if (animDictsLoaded.Count > 0)
                    foreach (var s in animDictsLoaded)
                    {
                        Utils.UnloadAnimDict(s);
                    }

                if (animSetsLoaded.Count > 0)
                    foreach (var s in animSetsLoaded)
                    {
                        Utils.UnloadAnimSet(s);
                    }

                callout.EndCallout();
            }
            catch (Exception err2)
            {
                // Do nothing
            }
        }

        public static async Task TaskPedTakeTargetPedHostage(Ped ped, Ped targetPed)
        {
            // Define parameters
            string suspectAnimDict = "anim@gangops@hostage@";
            string suspectAnimSet = "perp_idle";
            string victimAnimDict = suspectAnimDict;
            string victimAnimSet = "victim_idle";
            // Request memory resources
            await RequestAnimDict(suspectAnimDict);
            await RequestAnimDict(victimAnimDict);
            RequestAnimSet(suspectAnimSet);
            RequestAnimSet(victimAnimSet);
            //Debug.WriteLine("After await anims");
            // Code
            if (ped == null)
                //Debug.WriteLine("Ped is null");
                if (targetPed == null)
                    //Debug.WriteLine("Target is null");
                    ped.Task.ClearSecondary();
            ped.Detach();

            API.AttachEntityToEntity(targetPed.Handle, ped.Handle, 0, -0.24f, 0.11f, 0f, 0.5f, 0.5f, 0f, false, false,
                false, false, 2, false);
            ped.Task.PlayAnimation(suspectAnimDict, suspectAnimSet, 8f, -8f, -1, AnimationFlags.Loop, 1f);
            targetPed.Task.PlayAnimation(victimAnimDict, victimAnimSet, 8f, -8f, -1, AnimationFlags.Loop, 1f);
        }

        public static void ReleaseAnims()
        {
            foreach (var s in animDictsLoaded)
            {
                API.RemoveAnimDict(s);
            }
        }


        public static async Task RequestAnimDict(string animDict)
        {
            if (!API.HasAnimDictLoaded(animDict))
                API.RequestAnimDict(animDict);
            while (!API.HasAnimDictLoaded(animDict))
                await BaseScript.Delay(100);
            if (!animDictsLoaded.Contains(animDict))
                animDictsLoaded.Add(animDict);
        }

        public static async Task RequestAnimSet(string animSet)
        {
            if (!API.HasAnimSetLoaded(animSet))
                API.RequestAnimSet(animSet);
            while (!API.HasAnimSetLoaded(animSet))
                await BaseScript.Delay(100);
            if (!animSetsLoaded.Contains(animSet))
                animSetsLoaded.Add(animSet);
        }

        public static void UnloadAnimDict(string animDict)
        {
            API.RemoveAnimDict(animDict);
            animDictsLoaded.Remove(animDict);
        }

        public static void UnloadAnimSet(string animSet)
        {
            API.RemoveAnimSet(animSet);
            animSetsLoaded.Remove(animSet);
        }

        public static List<Entity> EntitiesInMemory = new List<Entity>();

        public static async Task<Ped> SpawnPedOneSync(PedHash pedHash, Vector3 location, [Optional] bool keepTask,
            [Optional] float heading)
        {
            Ped ped = await World.CreatePed(new(pedHash), location, heading);
            ped.IsPersistent = true;
            EntitiesInMemory.Add(ped);
            if (keepTask)
            {
                ped.AlwaysKeepTask = true;
                ped.BlockPermanentEvents = true;
            }

            return ped;
        }

        public static async Task<Vehicle> SpawnVehicleOneSync(VehicleHash vehicleHash, Vector3 location,
            float heading = 0f)
        {
            Vehicle veh = await World.CreateVehicle(new(vehicleHash), location, heading);
            if (veh == null) return null;
            veh.IsPersistent = true;
            EntitiesInMemory.Add(veh);
            return veh;
        }

        public static void ReleaseEntity(Entity ent)
        {
            if (EntitiesInMemory.Contains(ent))
            {
                if (ent.Model.IsPed)
                {
                    Ped ped = (Ped)ent;
                    ped.AlwaysKeepTask = false;
                    ped.BlockPermanentEvents = false;
                }

                ent.IsPersistent = false;
                EntitiesInMemory.Remove(ent);
            }
        }

        public static Vector4 GetRandomLocationFromArray(Array array)
        {
            var chance = new Random().Next(array.Length);
            var result = array.GetValue(chance);
            return (Vector4)result;
        }

        public static List<Vector4> GetLocationArrayFromJArray(JArray jObject, string name)
        {
            List<Vector4> locationArray = new List<Vector4>();
            var locationsObject = (JArray)jObject;
            foreach (JObject jToken in locationsObject)
            {
                locationArray.Add(JSONCoordsToVector4((JObject)jToken[name]));
            }

            return locationArray;
        }

        public static JObject GetChildFromParent(JToken parent, string name)
        {
            return (JObject)parent[name];
        }

        /*public static JObject GetConfig()
        {
            JObject result = null;
            try
            {
                string data = API.LoadResourceFile("fivepd", "callouts/Kilo247Robbery/settings.json");
                result = JObject.Parse(data);
            }
            catch (Exception err)
            {
                Debug.WriteLine(
                    "^8ERROR^7: ^3Please add the following line into the 'files' section of your fivepd fxmanifest.lua:");
                Debug.WriteLine(
                    "^1===================================================================================================");
                Debug.WriteLine("^9'callouts/Kilo247Robbery/*.json'");
                Debug.WriteLine(
                    "^1===================================================================================================");
                Debug.WriteLine(
                    "^8ERROR (Kilo's 24/7 Robbery Callout)^7: ^2 The above line will allow the callout to read the json files here in the callout folder!");
            }

            return result;
        }*/

        public static Vector3 GetRandomLocationInRadius(Vector3 pos, int min, int max, bool isPed = true)
        {
            int distance = new Random().Next(min, max);
            float offsetX = new Random().Next(-1 * distance, distance);
            float offsetY = new Random().Next(-1 * distance, distance);
            if (isPed)
                return World.GetNextPositionOnSidewalk(pos.ApplyOffset(new Vector3(offsetX, offsetY, 0)));
            else
                return World.GetNextPositionOnStreet(pos.ApplyOffset(new Vector3(offsetX, offsetY, 0)));
        }

        public static async Task PedTaskSassyChatAnimation(Ped ped)
        {
            if (!API.HasAnimDictLoaded("oddjobs@assassinate@vice@hooker"))
                API.RequestAnimDict("oddjobs@assassinate@vice@hooker");
            ////Debug.WriteLine("Requesting anim dict");
            if (!API.HasAnimSetLoaded("argue_b"))
                API.RequestAnimSet("argue_b");
            ////Debug.WriteLine("Requesting anim set");
            while (!API.HasAnimDictLoaded("oddjobs@assassinate@vice@hooker"))
                await BaseScript.Delay(200);
            ////Debug.WriteLine("Loaded anim dict");

            ////Debug.WriteLine("Loaded anim set");
            ////Debug.WriteLine("after waiting load");
            ped.Task.ClearAllImmediately();
            ped.Task.PlayAnimation("oddjobs@assassinate@vice@hooker", "argue_b", 8f, 8f, 10000, AnimationFlags.Loop,
                1f);
        }

        public static async Task<Blip> CreateLocationBlip(Vector3 pos, float radius = 5f, bool showRoute = true,
            BlipColor color = BlipColor.Yellow,
            BlipSprite sprite = BlipSprite.BigCircle, string name = "", bool isRadius = true,
            bool attachToEntity = false, Entity entityToAttachTo = null, bool isFlashing = false)
        {
            Blip blip;
            if (!attachToEntity)
            {
                blip = isRadius ? World.CreateBlip(pos, radius) : World.CreateBlip(pos);
            }
            else
            {
                if (entityToAttachTo == null) return null;
                blip = entityToAttachTo.AttachBlip();
            }

            //Debug.WriteLine(color.ToString());
            blip.Sprite = sprite;
            blip.Name = name;
            blip.IsFlashing = isFlashing;
            blip.Color = color;
            blip.ShowRoute = showRoute;
            blip.Alpha = 80;

            //Debug.WriteLine(blip.Color.ToString());

            return blip;
        }

        public static async Task KeepTaskEnterVehicle(Ped ped, Vehicle veh, VehicleSeat targetSeat)
        {
            SetIntoVehicleAfterTimer(ped, veh, VehicleSeat.Any, 30000);
            while (true)
            {
                Vector3 startPos = ped.Position;
                await BaseScript.Delay(2500);
                if (!ped.IsInVehicle(veh) && ped.Position.DistanceTo(startPos) < 1f)
                    ped.Task.EnterVehicle(veh, targetSeat);
                await BaseScript.Delay(2500);
            }
        }

        public static async Task WaitUntilPedIsInVehicle(Ped ped, Vehicle veh, [Optional] VehicleSeat targetSeat)
        {
            if (targetSeat != null)
            {
                while (true)
                {
                    if (ped.IsInVehicle(veh) || ped.IsInVehicle() && ped.SeatIndex == targetSeat)
                        return;
                    await BaseScript.Delay(500);
                }
            }
            else
            {
                while (true)
                {
                    if (ped.IsInVehicle(veh))
                        return;
                    await BaseScript.Delay(500);
                }
            }
        }

        public static async Task WaitUntilPedIsNotInVehicle(Ped ped, Vehicle veh = null)
        {
            if (!ped.IsInVehicle()) return;
            Vehicle targetVeh = veh != null ? veh : ped.CurrentVehicle;
            while (ped.IsInVehicle(targetVeh))
                await BaseScript.Delay(100);
        }

        public static async Task SetIntoVehicleAfterTimer(Ped ped, Vehicle veh, VehicleSeat targetSeat, int ms)
        {
            await BaseScript.Delay(ms);
            if (!ped.IsInVehicle(veh))
            {
                ped.SetIntoVehicle(veh, targetSeat);
            }
        }

        public static async Task TaskVehiclePark(Ped ped, Vehicle vehicle, Vector3 pos, float radius,
            bool keepEngineRunning = false, int timeoutAfterDuration = 30000)
        {
            pos = pos.Around(5f);
            bool stillWorking = true;
            var wait = new Action(async () =>
            {
                while (true)
                {
                    if (!vehicle.IsEngineRunning)
                    {
                        stillWorking = false;
                        return;
                    }

                    if (!ped.IsInVehicle())
                    {
                        stillWorking = false;
                        return;
                    }

                    await BaseScript.Delay(100);
                }
            });
            wait();
            if (!stillWorking) return;
            while (stillWorking)
            {
                pos = pos.Around(5f);
                API.TaskVehiclePark(ped.Handle, vehicle.Handle, pos.X, pos.Y,
                    pos.Z, vehicle.Heading, 1, 40f, false);
                await BaseScript.Delay(30000);
                if (vehicle.IsEngineRunning)
                {
                    pos = pos.Around(5f);
                    API.TaskVehiclePark(ped.Handle, vehicle.Handle, pos.X, pos.Y,
                        pos.Z, vehicle.Heading, 1, 40f, false);
                    await BaseScript.Delay(30000);
                    if (!stillWorking) return;
                    if (vehicle.IsEngineRunning)
                    {
                        ped.Task.ClearAll();
                        ped.Task.LeaveVehicle();
                        vehicle.IsEngineRunning = false;
                    }
                }
            }

            if (!stillWorking) return;
        }

        public enum GoToType
        {
            Run,
            Walk
        }

        public static async Task KeepTaskGoToForPed(Ped ped, Vector3 pos, float buffer = 2f,
            GoToType type = GoToType.Walk)
        {
            Vector3 startPos = ped.Position;
            switch (type)
            {
                case GoToType.Walk:
                {
                    ped.Task.GoTo(pos);
                    break;
                }
                case GoToType.Run:
                {
                    ped.Task.RunTo(pos);
                    break;
                }
                default:
                {
                    ped.Task.GoTo(pos);
                    break;
                }
            }
            while (ped.Position.DistanceTo(pos) > buffer)
            {
                await BaseScript.Delay(1000);
                if (ped.Position == startPos)
                {
                    switch (type)
                    {
                        case GoToType.Walk:
                        {
                            ped.Task.GoTo(pos);
                            break;
                        }
                        case GoToType.Run:
                        {
                            ped.Task.RunTo(pos);
                            break;
                        }
                        default:
                        {
                            ped.Task.GoTo(pos);
                            break;
                        }
                    }
                }
                await BaseScript.Delay(1000);
            }
        }

        public static async Task WaitUntilEntityIsAtPos(Entity ent, Vector3 pos, float buffer, int timeout = 60000)
        {
            if (ent == null || !ent.Exists())
                return;
            bool waitDB = false;
            bool stillWorking = true;
            var wait = new Action(async () =>
            {
                if (waitDB) return;
                waitDB = true;
                await BaseScript.Delay(60000);
                if (!stillWorking) return;
                ent.Position = pos;
                waitDB = false;
            });
            while (true)
            {
                if (ent == null || !ent.Exists())
                {
                    stillWorking = false;
                    return;
                }

                if (ent.Position.DistanceTo(pos) < buffer)
                {
                    stillWorking = false;
                    return;
                }

                wait();
                await BaseScript.Delay(200);
            }
        }

        public static PedHash GetRandomPed()
        {
            return RandomUtils.GetRandomPed(exclusions);
        }

        public static Vector3 JSONCoordsToVector3(JObject coordsObj)
        {
            ////Debug.WriteLine(coordsObj.ToString());
            Vector3 result = new Vector3((float)coordsObj["x"], (float)coordsObj["y"], (float)coordsObj["z"]);
            ////Debug.WriteLine(result.ToString());
            return result;
        }

        public static Vector4 JSONCoordsToVector4(JObject coordsObj)
        {
            ////Debug.WriteLine("Before x");
            float x = (float)coordsObj["x"];
            float y = (float)coordsObj["y"];
            float z = (float)coordsObj["z"];
            float w = (float)coordsObj["w"];
            ////Debug.WriteLine("Before return"+w.ToString());
            return new Vector4(x, y, z, w);
        }

        public static void KeepTask(Ped ped)
        {
            if (ped == null || !ped.Exists()) return;
            ped.IsPersistent = true;
            ped.AlwaysKeepTask = true;
            ped.BlockPermanentEvents = true;
        }

        public static List<Ped> keepTaskAnimation = new List<Ped>();

        public static async Task KeepTaskPlayAnimation(Ped ped, string animDict, string animSet,
            AnimationFlags flags = AnimationFlags.Loop)
        {
            if (keepTaskAnimation.Contains(ped))
                await StopKeepTaskPlayAnimation(ped);
            ped.Task.PlayAnimation(animDict, animSet);
            keepTaskAnimation.Add(ped);
            while (keepTaskAnimation.Contains(ped))
            {
                if (ped == null || ped.IsDead || ped.IsCuffed) break;

                if (!API.IsEntityPlayingAnim(ped.Handle, animDict, animSet, 3))
                {
                    //Debug.WriteLine(animDict + ", " +animSet);
                    ped.Task.PlayAnimation(animDict, animSet, 8f, 8f, -1, flags, 1f);
                }

                await BaseScript.Delay(1000);
            }
        }

        public static async Task StopKeepTaskPlayAnimation(Ped ped)
        {
            while (keepTaskAnimation.Contains(ped))
            {
                keepTaskAnimation.Remove(ped);
                await BaseScript.Delay(100);
            }
        }

        public static void UnKeepTask(Ped ped)
        {
            ped.IsPersistent = false;
            ped.AlwaysKeepTask = false;
            ped.BlockPermanentEvents = false;
        }

        public static Vector3[] ConvenienceLocations = new Vector3[]
        {
            new(-712.12f, -913.06f, 19.22f),
            new(29.49f, -1346.94f, 29.5f),
            new(-50.78f, -1753.61f, 29.42f),
            new(376.4f, 325.75f, 103.57f),
            new(-1223.94f, -906.52f, 12.33f)
        };

        public static Vector3[] HomeLocations = new Vector3[]
        {
            new(-120.15f, -1574.39f, 34.18f),
            new(-148.07f, -1596.64f, 38.21f),
            new(-32.44f, -1446.5f, 31.89f),
            new(-14.11f, -1441.93f, 31.1f),
            new(72.21f, -1938.59f, 21.37f),
            new(126.68f, -1930.01f, 21.38f),
            new(270.2f, -1917.19f, 26.18f),
            new(325.68f, -2050.86f, 20.93f),
            new(1099.52f, -438.65f, 67.79f),
            new(1046.24f, -498.14f, 64.28f),
            new(980.1f, -627.29f, 59.24f),
            new(943.45f, -653.49f, 58.43f),
            new(1223.08f, -696.85f, 60.8f),
            new(1201.06f, -575.68f, 69.14f),
            new(1265.9f, -648.33f, 67.92f),
            new(1241.5f, -566.4f, 69.66f),
            new(1204.73f, -557.74f, 69.62f),
            new(1223.06f, -696.74f, 60.81f),
            new(930.88f, -244.82f, 69.0f),
            new(880.01f, -205.01f, 71.98f),
            new(798.39f, -158.83f, 74.89f),
            new(820.86f, -155.84f, 80.75f), // Second floor
            new(208.65f, 74.53f, 87.9f),
            new(119.34f, 494.13f, 147.34f),
            new(79.74f, 486.13f, 148.2f),
            new(151.2f, 556.09f, 183.74f),
            new(232.1f, 672.06f, 189.98f),
            new(-66.76f, 490.13f, 144.88f),
            new(-175.94f, 502.73f, 137.42f),
            new(-230.26f, 488.29f, 128.77f),
            new(-355.91f, 469.56f, 112.61f),
            new(-353.17f, 423.13f, 110.98f),
            new(-312.53f, 474.91f, 111.83f),
            new(-348.99f, 514.99f, 120.65f),
            new(-376.59f, 547.66f, 123.85f),
            new(-406.6f, 566.28f, 124.61f),
            new(-520.28f, 594.07f, 120.84f),
            new(-581.37f, 494.04f, 108.26f),
            new(-678.67f, 511.67f, 113.53f),
            new(-784.46f, 459.47f, 100.25f),
            new(-824.67f, 422.6f, 92.13f),
            new(-881.97f, 364.1f, 85.36f),
            new(-967.59f, 436.88f, 80.57f),
            new(-1570.71f, 23.0f, 59.55f),
            new(-1629.9f, 36.25f, 62.94f),
            new(-1750.22f, -695.19f, 11.75f),
            new(-1270.03f, -1296.53f, 4.0f),
            new(-1148.96f, -1523.2f, 10.63f),
            new(-1105.61f, -1596.67f, 4.61f)
        };

        public static bool IsPedNonLethalOrMelee(Ped ped)
        {
            WeaponHash weapon = ped.Weapons.Current;
            return nonlethals.Contains(weapon) || melee.Contains(weapon);
        }

        public static WeaponHash[] nonlethals =
        {
            WeaponHash.Ball,
            WeaponHash.Parachute,
            WeaponHash.Flare,
            WeaponHash.Snowball,
            WeaponHash.Unarmed,
            WeaponHash.StunGun,
            WeaponHash.FireExtinguisher
        };

        public static WeaponHash[] melee =
        {
            WeaponHash.Crowbar,
            WeaponHash.Bat,
            WeaponHash.Bottle,
            WeaponHash.Flashlight,
            WeaponHash.Hatchet,
            WeaponHash.Knife,
            WeaponHash.Machete,
            WeaponHash.Nightstick,
            WeaponHash.Unarmed,
            WeaponHash.PoolCue,
            WeaponHash.StoneHatchet
        };

        public static VehicleHash GetRandomVehicleForRobberies()
        {
            return RandomUtils.GetRandomVehicle(FourPersonVehicleClasses);
        }

        public static IEnumerable<VehicleClass> FourPersonVehicleClasses = new List<VehicleClass>()
        {
            VehicleClass.Compacts,
            VehicleClass.Sedans,
            VehicleClass.Vans,
            VehicleClass.SUVs
        };

        public static PedHash GetRandomSuspect()
        {
            return suspects[new Random().Next(suspects.Length - 1)];
        }

        public static WeaponHash GetRandomWeapon()
        {
            int index = new Random().Next(weapons.Length);
            return weapons[index];
        }

        public static WeaponHash[] weapons =
        {
            WeaponHash.AssaultRifle,
            WeaponHash.PumpShotgun,
            WeaponHash.CombatPistol
        };

        public static PedHash[] suspects =
        {
            PedHash.MerryWeatherCutscene,
            PedHash.Armymech01SMY,
            PedHash.MerryWeatherCutscene,
            PedHash.ChemSec01SMM,
            PedHash.Blackops01SMY,
            PedHash.CiaSec01SMM,
            PedHash.PestContDriver,
            PedHash.PestContGunman,
            PedHash.TaoCheng,
            PedHash.Hunter,
            PedHash.EdToh,
            PedHash.PrologueMournMale01,
            PedHash.PoloGoon01GMY
        };

        public static IEnumerable<WeaponHash> weapExclusions = new List<WeaponHash>
        {
            WeaponHash.Ball,
            WeaponHash.Bat,
            WeaponHash.Snowball,
            WeaponHash.RayMinigun,
            WeaponHash.RayCarbine,
            WeaponHash.BattleAxe,
            WeaponHash.Bottle,
            WeaponHash.BZGas,
            WeaponHash.Crowbar,
            WeaponHash.Dagger,
            WeaponHash.FireExtinguisher,
            WeaponHash.Firework,
            WeaponHash.Flare,
            WeaponHash.FlareGun,
            WeaponHash.Flashlight,
            WeaponHash.GolfClub,
            WeaponHash.Grenade,
            WeaponHash.GrenadeLauncher,
            WeaponHash.Gusenberg,
            WeaponHash.Hammer,
            WeaponHash.Hatchet,
            WeaponHash.StoneHatchet,
            WeaponHash.StunGun,
            WeaponHash.Musket,
            WeaponHash.HeavySniper,
            WeaponHash.HeavySniperMk2,
            WeaponHash.HomingLauncher,
            WeaponHash.Knife,
            WeaponHash.KnuckleDuster,
            WeaponHash.Machete,
            WeaponHash.Molotov,
            WeaponHash.Nightstick,
            WeaponHash.NightVision,
            WeaponHash.Parachute,
            WeaponHash.PetrolCan,
            WeaponHash.PipeBomb,
            WeaponHash.PoolCue,
            WeaponHash.ProximityMine,
            WeaponHash.Railgun,
            WeaponHash.RayPistol,
            WeaponHash.RPG,
            WeaponHash.SmokeGrenade,
            WeaponHash.SniperRifle,
            WeaponHash.StickyBomb,
            WeaponHash.SwitchBlade,
            WeaponHash.Unarmed,
            WeaponHash.Wrench
        };

        public static IEnumerable<PedHash> exclusions = new List<PedHash>()
        {
            PedHash.Acult01AMM,
            PedHash.Motox01AMY,
            PedHash.Boar,
            PedHash.Cat,
            PedHash.ChickenHawk,
            PedHash.Chimp,
            PedHash.Chop,
            PedHash.Cormorant,
            PedHash.Cow,
            PedHash.Coyote,
            PedHash.Crow,
            PedHash.Deer,
            PedHash.Dolphin,
            PedHash.Fish,
            PedHash.Hen,
            PedHash.Humpback,
            PedHash.Husky,
            PedHash.KillerWhale,
            PedHash.MountainLion,
            PedHash.Pig,
            PedHash.Pigeon,
            PedHash.Poodle,
            PedHash.Rabbit,
            PedHash.Rat,
            PedHash.Retriever,
            PedHash.Rhesus,
            PedHash.Rottweiler,
            PedHash.Seagull,
            PedHash.HammerShark,
            PedHash.TigerShark,
            PedHash.Shepherd,
            PedHash.Stingray,
            PedHash.Westy,
            PedHash.BradCadaverCutscene,
            PedHash.Orleans,
            PedHash.OrleansCutscene,
            PedHash.ChiCold01GMM,
            PedHash.DeadHooker,
            PedHash.Marston01,
            PedHash.Niko01,
            PedHash.PestContGunman,
            PedHash.Pogo01,
            PedHash.Ranger01SFY,
            PedHash.Ranger01SMY,
            PedHash.RsRanger01AMO,
            PedHash.Zombie01,
            PedHash.Corpse01,
            PedHash.Corpse02,
            PedHash.Stripper01Cutscene,
            PedHash.Stripper02Cutscene,
            PedHash.StripperLite,
            PedHash.Stripper01SFY,
            PedHash.Stripper02SFY,
            PedHash.StripperLiteSFY
        };
    }
}