using System;
using System.Collections.Generic;
using System.Linq;
using MintStatSort;

internal static class FeatureChecks
{
    internal static void Run(Action<bool,string> check)
    {
        check(BatchCount.Clamp(0)==1 && BatchCount.Clamp(1000)==999, "Recipe execution range is bounded");
        check(BatchCount.TryParse("999",out var number) && number==999, "Maximum recipe execution count is accepted");
        foreach(var invalid in new[]{"", "0", "1000", "-1", "1.5", "1e2", "NaN"})
            check(!BatchCount.TryParse(invalid,out _), "Invalid execution count is rejected: "+invalid);
        check(BatchCount.Required(73,3)==219, "Batch ingredients multiply rounded per-execution quantities");
        check(BatchCount.Required(null,3)==null && BatchCount.Required(-1,3)==null, "Unknown requirements stay unknown for batches");
        check(BatchCount.Required(int.MaxValue,999)==null && BatchCount.Required(0,999)==0, "Batch multiplication cannot overflow or turn known zero into unknown");
        var layout=new ComparisonLayout(420,new[]{80f,260f,134f});
        check(layout.Width(0)==80 && layout.Width(1)==260 && layout.X(2)==layout.NameWidth+340, "Custom column widths produce non-overlapping positions");
        var sanitized=new ComparisonLayout(900,new[]{float.NaN, float.PositiveInfinity,10f,1000f});
        check(sanitized.Width(0)==134 && sanitized.Width(1)==134 && sanitized.Width(2)==80 && sanitized.Width(3)==260,"Invalid column widths are sanitized");
        var doc=new ProfileDocument { Name="Armaduras y prendas ñ", Category=CategorySelection.Pack(new[]{"kind:armor","kind:clothing"}),
            OnlyStockReady=true,Executions=3,Quality=4,Material="Cloth",OrderMode="name",
            Criteria=new List<ProfileMetric>{new ProfileMetric{Id="stat:Mass",Higher=false,Weight=3,HasMaximum=true,Maximum=6.25}},
            Columns=new List<ProfileColumn>{new ProfileColumn{Id="stat:Mass",Width=240,Visible=false}}};
        var xml=doc.Serialize(); var copy=ProfileDocument.Parse(xml);
        check(copy.Name==doc.Name && copy.Category==doc.Category && copy.Material=="Cloth" && copy.Executions==3 && copy.OnlyStockReady && copy.Quality==4 && copy.OrderMode=="name", "Portable XML preserves category union, comparison context, quantities and stock filter");
        check(copy.Criteria.Single().Maximum==6.25 && copy.Criteria.Single().Weight==3 && !copy.Criteria.Single().Higher && copy.Columns.Single().Width==240 && !copy.Columns.Single().Visible,"Portable XML preserves numeric limits, importance and hidden-column layout");
        copy.Columns[0].Width=80; copy.Criteria[0].Weight=1;
        check(doc.Columns[0].Width==240 && doc.Criteria[0].Weight==3,"Parsed profile cannot mutate its source document");
        foreach(var bad in new[]{
            "<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///private'>]><MintStatSortProfile format='1'><name>&a;</name></MintStatSortProfile>",
            xml.Replace("format=\"1\"","format=\"2\""), xml.Replace("<executions>3</executions>","<executions>1000</executions>"),
            xml.Replace("maximum=\"6.25\"","maximum=\"NaN\""), xml.Replace("weight=\"3\"","weight=\"6\""),
            xml.Replace("width=\"240\"","width=\"500\""), xml.Replace("<quality>4</quality>","<quality>-1</quality>"),
            xml.Replace("</criteria>","<criterion id='stat:Mass'/></criteria>"),
            xml.Replace("</columns>","<column id='stat:Mass'/></columns>"), new string('x',ProfileDocument.MaximumLength+1),
            "<MintStatSortProfile format='1'><name></name></MintStatSortProfile>"})
        {
            bool rejected=false; try{ProfileDocument.Parse(bad);}catch(Exception){rejected=true;}
            check(rejected,"Malformed, unsafe or out-of-range profile is rejected before import");
        }
        doc.Criteria[0].Minimum=7; doc.Criteria[0].HasMinimum=true;
        bool exportRejected=false; try{doc.Serialize();}catch(FormatException){exportRejected=true;}
        check(exportRejected,"Invalid minimum/maximum range cannot be exported");
    }
}
