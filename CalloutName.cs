using System.Drawing;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePD.API;
using Kilo.Commons.Utils;

namespace CalloutTemplate
{
    public class CalloutName : Callout
    {
        public CalloutName()
        {
            ShortName = "CalloutName";
            CalloutDescription = "";
            StartDistance = 120f;
            ResponseCode = 3;
        }

        public override Task OnAccept()
        {
            return base.OnAccept();
        }
        
        public override void OnStart(Ped closest)
        {
            base.OnStart(closest);
        }

        public override void OnCancelBefore()
        {
            base.OnCancelBefore();
        }
    }
}