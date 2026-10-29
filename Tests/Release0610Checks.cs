using System;
using System.Collections.Generic;
using System.Linq;
using MintStatSort;
internal static class Release0610Checks
{
    internal static void Run(Action<bool, string> check)
    {
        var a = new[] { 10d, 30d }; var b = new[] { 20d, 20d }; var c = new[] { 30d, 10d };
        var ranked = SortingCore.Balanced(new[] { a,b,c }, r => r.Select(v => (double?)v).ToArray(), r => "", new[] {true,true},true,new[]{2,1});
        var middle=ranked.Single(r => ReferenceEquals(r.Item,b));
        check(middle.Desirabilities.SequenceEqual(new double?[]{.5,.5}) && Math.Abs(middle.WeightShares.Sum()-1)<1e-10,
            "Score explanation reflects both normalized criteria and the actual weight shares");
        check(Math.Abs(middle.Score.Value-50)<1e-10 && middle.WeightShares[0] > middle.WeightShares[1],
            "Displayed weighted geometric explanation agrees with the score");
        check(ranked.Where(r => !ReferenceEquals(r.Item,b)).All(r => r.Score == 0), "A minimum criterion cannot be hidden by another maximum");
        var constant=SortingCore.Balanced(new[]{a,a}, r=>r.Select(v=>(double?)v).ToArray(),r=>"",new[]{true,true},true);
        check(constant.All(r=>r.Score==100 && r.Desirabilities.All(v=>v==null) && r.WeightShares.All(v=>v==0)), "Constant criteria have no weight in explanation");
        var reservations=new Dictionary<string,long>(); var mix=new SubstituteAllocation<string>();
        mix.Add("steel",5,10,reservations); mix.Add("plasteel",3,6,reservations);
        check(mix.Complete && mix.Remaining==0 && reservations["steel"]==5 && reservations["plasteel"]==3,
            "Substitutes with different requirements can complete one ingredient without changing inventories");
        var second=new SubstituteAllocation<string>();second.Add("steel",5,10,reservations);
        check(!second.Complete && second.Available==0, "A stack reserved by an overlapping ingredient cannot count twice");
        var huge=new SubstituteAllocation<string>();huge.Add("bulk",int.MaxValue,1,new Dictionary<string,long>());
        check(huge.Complete && huge.Available==int.MaxValue, "Large stacks do not overflow substitute fractions");
        var rounding=new SubstituteAllocation<string>();rounding.Add("s1",2,3,new Dictionary<string,long>());rounding.Add("s2",1,3,new Dictionary<string,long>());
        check(rounding.Complete, "Fractional requirements finish despite floating-point roundoff");
        check(Math.Abs(SustainedDamage.Calculate(10,1,10,3,1,1,.1,2).Value-100/10.6)<1e-10,
            "Sustained DPS includes a partial last burst and a full magazine reload");
        check(SustainedDamage.Calculate(10,1,10,3,1,1,.1,20) < SustainedDamage.Calculate(10,1,10,3,1,1,.1,2),
            "Per-cartridge reloads reduce sustained DPS when the entire magazine must be filled");
        check(SustainedDamage.Calculate(1,1,0,1,0,0,0,1)==null && SustainedDamage.Calculate(1,1,1,1,0,0,0,0)==null &&
            SustainedDamage.Calculate(double.NaN,1,1,1,0,0,0,1)==null, "Invalid or unknown sustained cycles remain missing");
        var document=new ProfileDocument{Name="New references",Ammo="Ammo_Test",MixSubstitutes=true};
        var restored=ProfileDocument.Parse(document.Serialize());
        check(restored.Ammo==document.Ammo && restored.MixSubstitutes, "Profiles exchange ammunition and substitute preference");
        restored=ProfileDocument.Parse("<MintStatSortProfile format='1'><name>Old profile</name></MintStatSortProfile>");
        check(restored.Ammo=="" && !restored.MixSubstitutes, "Existing profiles retain their reference behavior");
    }
}
