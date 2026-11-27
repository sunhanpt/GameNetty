using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CapybaraRebuild
{
    public sealed partial class Adventure
    {
        private Dictionary<int,EquipmentItem> equipmentCatalog;
        private static readonly int[] EquipmentSlotTypes={1,2,3,3,4,4};
        internal CampCatalog CampStock=>data.campCatalog;
        internal bool CampGemShopUnlocked
        {
            get
            {
                var gate=CampStock?.gemShopUnlock;
                return gate!=null && (Profile.highest>gate.chapter || Profile.cleared.Contains(gate.chapter) ||
                    Profile.chapterProgress.Any(row=>row.chapter==gate.chapter && row.stage>=gate.stage));
            }
        }
        internal EquipmentItem Equipment(int id)=>equipmentCatalog.TryGetValue(id,out var item)?item:null;

        private void InitializeEquipment()
        {
            equipmentCatalog=(data.campCatalog?.equipment??Array.Empty<EquipmentItem>()).ToDictionary(item=>item.id);
            Profile.equipmentChestProgress??=new List<EquipmentChestProgress>();
            if(Profile.wornEquipment==null || Profile.wornEquipment.Length==0)
            {
                Profile.wornEquipment=new int[EquipmentSlotTypes.Length];
                foreach(var gear in data.gear.Take(Profile.gearLevel)) AddLegacyEquipment(gear.id);
            }
            else if(Profile.wornEquipment.Length!=EquipmentSlotTypes.Length)
            {
                var old=Profile.wornEquipment;
                Array.Resize(ref old,EquipmentSlotTypes.Length);
                Profile.wornEquipment=old;
            }
        }

        private void AddLegacyEquipment(int id)
        {
            int type=Equipment(id)?.type??Array.FindIndex(data.gear,gear=>gear.id==id)+1;
            int slot=Array.IndexOf(EquipmentSlotTypes,type);
            if(slot<0) return;
            if(ItemCount(id)==0) AddReward(Profile.inventory,new Reward{id=id,count=1});
            if(Profile.wornEquipment[slot]==0) Profile.wornEquipment[slot]=id;
        }

        internal int BackpackCount(int id)=>Math.Max(0,ItemCount(id)-Profile.wornEquipment.Count(worn=>worn==id));
        internal IEnumerable<EquipmentItem> BackpackEquipment=>Profile.inventory.Where(row=>BackpackCount(row.id)>0)
            .Select(row=>Equipment(row.id)).Where(item=>item!=null);

        internal bool ChangeEquipment(int slot,int id)
        {
            if(Run!=null || slot<0 || slot>=Profile.wornEquipment.Length || Profile.wornEquipment[slot]==id) return false;
            if(id!=0 && (Equipment(id)?.type!=EquipmentSlotTypes[slot] || BackpackCount(id)==0)) return false;
            Profile.wornEquipment[slot]=id;
            Commit();
            return true;
        }

        internal static bool FitsEquipmentSlot(int slot,EquipmentItem item)=>EquipmentSlotTypes[slot]==item.type;

        internal (int currency,int cost,int draws) ChestOffer(EquipmentChest chest)
        {
            var progress=ChestDraws(chest);
            int remaining=chest.limitTimes-(progress?.draws??0);
            if(remaining<=0) return (chest.priceId,chest.singlePrice,0);
            if(chest.freeTimes>0 && (progress?.freeDay!=Today || progress.freeUses<chest.freeTimes)) return (0,0,1);
            int keys=ItemCount(chest.priceId);
            if(chest.tenPrice>0 && keys>=chest.tenPrice && remaining>=10) return (chest.priceId,chest.tenPrice,10);
            if(keys<chest.singlePrice && chest.singlePriceOrigin>0) return (2,chest.singlePriceOrigin,1);
            if(chest.quickDraw>0 && keys>=chest.singlePrice)
            {
                int draws=Math.Min(Math.Min(keys/chest.singlePrice,9),remaining);
                return (chest.priceId,draws*chest.singlePrice,draws);
            }
            return (chest.priceId,chest.singlePrice,1);
        }

        private string Today=>localDate().ToString("yyyyMMdd",CultureInfo.InvariantCulture);
        private EquipmentChestProgress ChestDraws(EquipmentChest chest)=>Profile.equipmentChestProgress.FirstOrDefault(row=>row.id==chest.id);

        internal Reward[] OpenEquipmentChest(EquipmentChest chest,int seed,(int currency,int cost,int draws) expected)
        {
            var actual=ChestOffer(chest);
            if(Run!=null || !CampShopUnlocked || actual!=expected || actual.draws==0 ||
               (actual.cost>0 && ItemCount(actual.currency)<actual.cost)) return Array.Empty<Reward>();

            var progress=ChestDraws(chest)??new EquipmentChestProgress{id=chest.id};
            var random=new Random(seed);
            var rewards=new List<Reward>();
            for(int draw=0;draw<actual.draws;draw++)
            {
                int index=progress.draws+draw;
                int pool=index<chest.first.Length?chest.first[index]:WeightedReward(chest.rates,random,false)?.id??0;
                var choices=CampStock.pools.FirstOrDefault(row=>row.id==pool)?.rewards;
                var item=choices==null?null:WeightedReward(choices,random,true);
                if(item==null) return Array.Empty<Reward>();
                AddReward(rewards,item);
            }
            if(chest.gold>0) AddReward(rewards,new Reward{id=1,count=chest.gold*actual.draws});
            if(actual.cost>0)
            {
                var currency=Profile.inventory.First(row=>row.id==actual.currency);
                currency.count-=actual.cost;
                if(currency.count==0) Profile.inventory.Remove(currency);
            }
            if(actual.currency==0)
            {
                if(progress.freeDay!=Today) progress.freeUses=0;
                progress.freeDay=Today;
                progress.freeUses++;
            }
            if(!Profile.equipmentChestProgress.Contains(progress)) Profile.equipmentChestProgress.Add(progress);
            progress.draws+=actual.draws;
            foreach(var reward in rewards)
            {
                if(reward.id==1) Profile.gold+=reward.count;
                else AddReward(Profile.inventory,reward);
            }
            Commit();
            return rewards.ToArray();
        }

        private static Reward WeightedReward(Reward[] values,Random random,bool pool)
        {
            int total=values.Sum(value=>pool?value.rate:value.count);
            if(total<=0) return null;
            int roll=random.Next(total);
            foreach(var value in values)
            {
                roll-=pool?value.rate:value.count;
                if(roll<0) return value;
            }
            return null;
        }
    }
}
