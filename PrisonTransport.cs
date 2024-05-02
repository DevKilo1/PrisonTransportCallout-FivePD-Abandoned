using System;
using System.Collections.Generic;
using System.Drawing;
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
    [CalloutProperties("Prison Transport","DevKilo","1.0")]
    public class PrisonTransport : Callout
    {
        public PrisonTransport()
        {
            try
            {
                InitInfo(Game.PlayerPed.Position);
                ShortName = "CalloutName";
                CalloutDescription = "";
                StartDistance = 120f;
                ResponseCode = 3;
            }
            catch (Exception ex)
            {
                Utils.CalloutError(ex, this);
            }
        }

        private JObject defaultConfig = new JObject()
        {
            
        };

        private Vehicle bus;
        private Ped driver;
        private List<Ped> prisoners = new List<Ped>();
        public async override Task OnAccept()
        {
            AcceptHandler();
        }

        private async Task AcceptHandler()
        {
            bus = await Utils.SpawnVehicleOneSync(VehicleHash.PBus, Location);
            int numberOfPrisoners = RandomUtils.GetRandomNumber(1, bus.PassengerCapacity);
            for (int i = 0; i < numberOfPrisoners; i++)
            {
                Ped p = await Utils.SpawnPedOneSync(Utils.GetRandomPed(), Location, true);
                if (p == null) continue;
                prisoners.Add(p);
                await BaseScript.Delay(0);
            }
        }
        
        public override void OnStart(Ped closest)
        {
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