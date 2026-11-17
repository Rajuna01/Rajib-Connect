
using System;
using System.Collections.Generic;

namespace AcxiomCRM.ViewModels
{
    public class ReportsViewModel
    {
        // Selected date filter
        public string Period { get; set; } = "All";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Customer statistics
        public int TotalCustomers { get; set; }
        public int ActiveCustomers { get; set; }
        public int InactiveCustomers { get; set; }

        // Lead statistics
        public int TotalLeads { get; set; }
        public int NewLeads { get; set; }
        public int ContactedLeads { get; set; }
        public int QualifiedLeads { get; set; }
        public int UnqualifiedLeads { get; set; }
        public int ConvertedLeads { get; set; }
        public int LostLeads { get; set; }
        public decimal TotalLeadValue { get; set; }
        public double LeadConversionRate { get; set; }

        // Opportunity statistics
        public int TotalOpportunities { get; set; }
        public int OpenOpportunities { get; set; }
        public int WonOpportunities { get; set; }
        public int LostOpportunities { get; set; }
        public decimal TotalPipelineValue { get; set; }
        public decimal WeightedPipelineValue { get; set; }
        public decimal WonRevenue { get; set; }

        // Follow-up statistics
        public int TotalFollowUps { get; set; }
        public int PlannedFollowUps { get; set; }
        public int CompletedFollowUps { get; set; }
        public int MissedFollowUps { get; set; }
        public int CancelledFollowUps { get; set; }

        // Activity statistics
        public int TotalActivities { get; set; }
        public int PlannedActivities { get; set; }
        public int CompletedActivities { get; set; }
        public int CancelledActivities { get; set; }

        // Monthly and employee reports
        public List<MonthlyReportItem> MonthlyTrends { get; set; } = new();
        public List<SalesExecutiveReportItem> SalesExecutivePerformance { get; set; } = new();
    }

    public class MonthlyReportItem
    {
        public string Label { get; set; } = string.Empty;
        public int Leads { get; set; }
        public int Opportunities { get; set; }
        public decimal WonOpportunityValue { get; set; }
    }

    public class SalesExecutiveReportItem
    {
        public string Name { get; set; } = string.Empty;
        public int Leads { get; set; }
        public int Opportunities { get; set; }
        public int WonOpportunities { get; set; }
        public decimal PipelineValue { get; set; }
        public decimal WonValue { get; set; }
    }
}
