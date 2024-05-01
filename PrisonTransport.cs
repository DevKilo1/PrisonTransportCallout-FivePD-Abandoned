using System.Drawing;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePD.API;
using Kilo.Commons.Utils;

namespace Callouts
{
    public class PrisonTransport : Callout
    {
        public PrisonTransport()
        {
            Utils.Draw3DText(Vector3.Zero, "");
            ShortName = "CalloutName";
            CalloutDescription = "";
            StartDistance = 120f;
            ResponseCode = 3;
        }

        public async override Task OnAccept()
        {
            
        }
        
        public override void OnStart(Ped closest)
        {
        }

        public override void OnCancelBefore()
        {
           
        }
    }
}