using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePD.API;
using FivePD.API.Utils;
using Kilo.Commons;
using Kilo.Commons.Config;
using Kilo.Commons.Utils;
using Newtonsoft.Json.Linq;

namespace Callouts
{
    [CalloutProperties("Prison Transport", "DevKilo", "1.0")]
    public class PrisonTransport : Callout
    {
        private Vector4 StartingLocation =
            new Vector4(1864.1049804688f, 3701.7260742188f, 33.779937744141f, 30.133010864258f); // Prison

        private Config config;

        private JObject locationObject;

        public PrisonTransport()
        {
            try
            {
                InitInfo(GetLocation());
                ShortName = "Prisoner Transport";
                CalloutDescription = "";
                StartDistance = 60f;
                ResponseCode = 1;
                config = new Config(AddonType.callouts, defaultConfig.ToString(), "PrisonerTransport");
            }
            catch (Exception ex)
            {
                Utils.CalloutError(ex, this);
            }
        }

        private Vector3 GetLocation()
        {
            JArray locations = (JArray)config["Locations"];

            JObject obj = (JObject)locations?[RandomUtils.GetRandomNumber(0, locations.Count)];
            locationObject = obj;
            return Utils.JSONCoordsToVector3((JObject)obj?["PoliceLocation"]);
        }


        private JObject defaultConfig = new JObject()
        {
            ["Locations"] = new JArray
            {
                new JObject()
                {
                    ["Name"] = "SandyPD To Bolingbroke",
                    ["SpawnLocation"] =
                    {
                        ["x"] = 2068.9677734375,
                        ["y"] = 3851.8784179688,
                        ["z"] = 33.896728515625,
                        ["w"] = 118.29257202148
                    },
                    ["PoliceLocation"] =
                    {
                        ["x"] = 1864.1049804688,
                        ["y"] = 3701.7260742188,
                        ["z"] = 33.779937744141,
                        ["w"] = 30.133010864258
                    },
                    ["PoliceLocationTeleportRadius"] = 10f,
                    ["BusPathIntoPrison"] = new JArray()
                    {
                        new JObject()
                        {
                            ["x"] = 1920.3337402344,
                            ["y"] = 2608.8679199219,
                            ["z"] = 46.470203399658,
                            ["Timeout"] = default,
                            ["Speed"] = default,
                            ["BufferDistance"] = 3f
                        },
                        new JObject()
                        {
                            ["x"] = 1852.5096435547,
                            ["y"] = 2608.3762207031,
                            ["z"] = 45.890480041504,
                            ["Timeout"] = default,
                            ["Speed"] = default,
                            ["BufferDistance"] = 3f
                        },
                        new JObject()
                        {
                            ["x"] = 1823.0548095703,
                            ["y"] = 2607.7556152344,
                            ["z"] = 45.842922210693,
                            ["Timeout"] = 3000,
                            ["Speed"] = default,
                            ["BufferDistance"] = 3f
                        },
                        new JObject()
                        {
                            ["x"] = 1801.20703125,
                            ["y"] = 2605.5842285156,
                            ["z"] = 45.82555770874,
                            ["Timeout"] = 3000,
                            ["Speed"] = default,
                            ["BufferDistance"] = 3f
                        },
                        new JObject()
                        {
                            ["x"] = 1720.9272460938,
                            ["y"] = 2606.5170898438,
                            ["z"] = 45.823463439941,
                            ["Timeout"] = default,
                            ["Speed"] = default,
                            ["BufferDistance"] = 3f
                        },
                        new JObject()
                        {
                            ["x"] = 1687.2510986328,
                            ["y"] = 2601.0256347656,
                            ["z"] = 45.823280334473,
                            ["Timeout"] = default,
                            ["Speed"] = default,
                            ["BufferDistance"] = 3f
                        }
                    }
                }
            }
        };

        private Utils.Waypoint[] ConvertPathArrayToWaypointPath(JArray pathArray, Entity entityToTrack)
        {
            // Direct children are JObjects.
            List<Utils.Waypoint> path = new List<Utils.Waypoint>();
            foreach (JObject obj in pathArray)
            {
                Vector3 coords = new((float)obj["x"], (float)obj["y"], (float)obj["z"]);
                int timeout = 100;
                float speed = 20f;
                float bufferDistance = 0.1f;
                if ((int)obj["Timeout"] != default && obj["Timeout"] is not null)
                {
                    timeout = (int)obj["Timeout"];
                }

                if ((float)obj["Speed"] != default && obj["Speed"] is not null)
                {
                    speed = (float)obj["Speed"];
                }

                if ((float)obj["BufferDistance"] != default && obj["BufferDistance"] is not null)
                {
                    bufferDistance = (float)obj["BufferDistance"];
                }

                path.Add(new Utils.Waypoint(coords, entityToTrack, timeout, bufferDistance));
            }

            return path.ToArray();
        }

        private Vehicle bus;
        private Ped driver;
        private List<Ped> prisoners = new List<Ped>();

        public async override Task OnAccept()
        {
            AcceptHandler();
        }

        private async Task<VehicleSeat> GetUnoccupiedPassengerSeat(Vehicle vehicle)
        {
            VehicleSeat seat = VehicleSeat.Any;
            while (!vehicle.IsSeatFree(seat))
            {
                seat = VehicleSeat.Any;
                await BaseScript.Delay(0);
            }

            return seat;
        }

        private Utils.Waypoint Waypoint(Vector3 pos, int timeout = 100)
        {
            return new Utils.Waypoint(pos, driver, timeout);
        }

        private Utils.Waypoint[] PathToGate;


        private async Task AcceptHandler()
        {
            Vector4 policeStationCoords = Utils.JSONCoordsToVector4((JObject)locationObject["PoliceLocation"]);
            Vector4 spawnCoords = Utils.JSONCoordsToVector4((JObject)locationObject["SpawnLocation"]);
            bus = await Utils.SpawnVehicleOneSync(VehicleHash.PBus, (Vector3)spawnCoords, spawnCoords.W);
            driver = await Utils.SpawnPedOneSync(PedHash.Prisguard01SMM, (Vector3)spawnCoords, true);
            driver.SetIntoVehicle(bus, VehicleSeat.Driver);
            PathToGate = ConvertPathArrayToWaypointPath((JArray)locationObject["BusPathIntoPrison"], bus);

            var toPoliceStation = new Utils.Waypoint((Vector3)policeStationCoords, bus, 100, 10f);
            toPoliceStation.Start();
            await Utils.WaitUntilEntityIsAtPos(bus, (Vector3)policeStationCoords, 20f, 120000);
            toPoliceStation.SetDrivingSpeed(5f);
            await toPoliceStation.Wait();
            bus.Position = (Vector3)policeStationCoords;
            bus.Heading = policeStationCoords.W;
            await BaseScript.Delay(5000);
            // TO-DO: Spawn prisoners at prisoner spawn location in line and have them get in the bus. (May 2nd 2024 @ 12:00 AM)
            
        }

        public override void OnStart(Ped closest)
        {
            Tick += UpdateLocation;
        }

        private Task UpdateLocation()
        {
            Vector3 targetLocation = bus.Position;
            Location = targetLocation;
            return Task.FromResult(0);
        }

        public override void OnCancelBefore()
        {
            foreach (var entity in Utils.EntitiesInMemory.ToArray())
            {
                if (entity == null) continue;
                if (entity.Model.IsPed)
                {
                    var ped = (Ped)entity;
                    if (ped.IsCuffed) continue;
                    if (ped.IsDead) continue;
                }

                entity.MarkAsNoLongerNeeded();
            }
        }
    }
}