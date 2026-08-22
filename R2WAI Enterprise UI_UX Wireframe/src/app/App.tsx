import React, { useState, useRef, useEffect } from "react";
import {
  LayoutDashboard, Bot, Zap, BookOpen, Plug, Wrench,
  FlaskConical, Rocket, Activity, Users, Settings,
  ChevronLeft, ChevronDown, ChevronUp, ChevronRight,
  X, Bell, Search, Plus, Edit, Trash2, Eye, Play,
  Filter, RefreshCw, CheckCircle, XCircle, AlertCircle,
  Clock, ArrowUp, ArrowDown, MoreHorizontal, Shield,
  Database, Server, Globe, FileText, FolderOpen,
  Calendar, Send, MessageSquare, Check, TrendingUp,
  User, LogOut, AlertTriangle, Copy, ExternalLink,
  ArrowRight, Save, Key, GitBranch, Code,
  Layers, Terminal, Download, Upload, Power,
  Menu, Info, BarChart2, Lock, Star,
  Radio, Circle, Minus, Cpu,
  HelpCircle, Sparkles, UserCheck,
  SlidersHorizontal as Sliders, Webhook,
} from "lucide-react";
import {
  AreaChart, Area, PieChart, Pie, Cell,
  XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
  BarChart, Bar, LineChart, Line,
} from "recharts";

// ─── Types ───────────────────────────────────────────────────────────────────
type Screen =
  | "login" | "dashboard" | "aiAssistants" | "aiDetail" | "aiPlayground"
  | "automations" | "automationWizard" | "workflowBuilder"
  | "knowledge" | "knowledgeEdit" | "integrations" | "integrationEdit"
  | "toolsApis" | "testPlayground" | "publish"
  | "monitor" | "executionDetails" | "usersRoles" | "permissionsMatrix" | "settings";
type Role = "superAdmin" | "admin" | "user";
type Drawer = null | "aiEdit" | "automationDetail" | "integrationEdit" | "knowledgeEdit";

// ─── Mock Data ────────────────────────────────────────────────────────────────
const execData = [
  { date: "12 May", execs: 820 }, { date: "13 May", execs: 932 },
  { date: "14 May", execs: 901 }, { date: "15 May", execs: 1045 },
  { date: "16 May", execs: 987 }, { date: "17 May", execs: 1123 },
  { date: "18 May", execs: 1248 },
];
const statusData = [
  { name: "Successful", value: 1230, color: "#16A34A" },
  { name: "Failed", value: 10, color: "#DC2626" },
  { name: "In Progress", value: 22, color: "#D97706" },
  { name: "Pending", value: 8, color: "#94A3B8" },
];
const weeklyData = [
  { day: "Mon", runs: 142 }, { day: "Tue", runs: 198 },
  { day: "Wed", runs: 165 }, { day: "Thu", runs: 234 },
  { day: "Fri", runs: 189 }, { day: "Sat", runs: 87 }, { day: "Sun", runs: 63 },
];

const assistants = [
  { id: 1, name: "Citizen Support", desc: "Handles citizen queries", channel: "Web", status: "Active", updated: "12 May 2025", runs: 456 },
  { id: 2, name: "Application Helper", desc: "Guides on applications", channel: "Mobile", status: "Active", updated: "14 May 2025", runs: 289 },
  { id: 3, name: "Grievance Assistant", desc: "Handles grievances", channel: "Web", status: "Inactive", updated: "10 May 2025", runs: 134 },
  { id: 4, name: "Policy Assistant", desc: "Explains policies", channel: "WhatsApp", status: "Active", updated: "11 May 2025", runs: 312 },
  { id: 5, name: "FAQ Assistant", desc: "Answers FAQs", channel: "Web", status: "Active", updated: "13 May 2025", runs: 201 },
];

const automations = [
  { id: 1, name: "Application Verification", trigger: "API", does: "Verify docs and notify applicant", status: "Active", lastRun: "10 min ago", success: 98.4 },
  { id: 2, name: "Document Expiry Alert", trigger: "Schedule", does: "Notify about expiring documents", status: "Active", lastRun: "1 hour ago", success: 97.2 },
  { id: 3, name: "Payment Confirmation", trigger: "Webhook", does: "Confirm and update payment status", status: "Active", lastRun: "3 hrs ago", success: 99.0 },
  { id: 4, name: "Grievance Escalation", trigger: "Event", does: "Auto-escalate unresolved grievances", status: "Active", lastRun: "5 hrs ago", success: 95.8 },
  { id: 5, name: "Welcome Notification", trigger: "AI Assistant", does: "Send welcome message to citizen", status: "Active", lastRun: "10 min ago", success: 99.1 },
  { id: 6, name: "Document Expiry Alert 2", trigger: "Schedule", does: "Notify officer if application pending", status: "Inactive", lastRun: "2 days ago", success: 80.2 },
];

const knowledgeBases = [
  { id: 1, name: "Citizen Services FAQ", type: "Document", docs: 142, status: "Active", updated: "10 May 2025" },
  { id: 2, name: "Policy Handbook 2025", type: "PDF", docs: 1, status: "Active", updated: "8 May 2025" },
  { id: 3, name: "Application Guidelines", type: "Website", docs: 58, status: "Active", updated: "5 May 2025" },
  { id: 4, name: "Benefits & Schemes DB", type: "Database", docs: 320, status: "Syncing", updated: "12 May 2025" },
  { id: 5, name: "Legal Reference Library", type: "Document", docs: 89, status: "Active", updated: "1 May 2025" },
];

const integrations = [
  { id: 1, name: "National ID Verification", category: "Identity", status: "Connected", lastSync: "2 min ago" },
  { id: 2, name: "SMS Gateway (Twilio)", category: "Communication", status: "Connected", lastSync: "5 min ago" },
  { id: 3, name: "Email Service", category: "Communication", status: "Connected", lastSync: "10 min ago" },
  { id: 4, name: "Payment Gateway", category: "Finance", status: "Error", lastSync: "2 hrs ago" },
  { id: 5, name: "Document Store (S3)", category: "Storage", status: "Connected", lastSync: "1 min ago" },
  { id: 6, name: "Legacy HR System", category: "Internal", status: "Disconnected", lastSync: "3 days ago" },
];

const tools = [
  { id: 1, name: "ID Lookup API", type: "REST API", endpoint: "/api/v2/id-lookup", auth: "API Key", calls: 1248, status: "Active" },
  { id: 2, name: "Document Validator", type: "REST API", endpoint: "/api/v1/validate", auth: "OAuth2", calls: 856, status: "Active" },
  { id: 3, name: "SMS Sender", type: "Webhook", endpoint: "/webhooks/sms", auth: "Bearer", calls: 2341, status: "Active" },
  { id: 4, name: "Email Dispatcher", type: "REST API", endpoint: "/api/v1/email", auth: "API Key", calls: 1102, status: "Active" },
  { id: 5, name: "Payment Processor", type: "REST API", endpoint: "/api/v3/payments", auth: "OAuth2", calls: 445, status: "Inactive" },
];

const executions = [
  { id: "EXE-1248", name: "Application Verification", trigger: "API", status: "Success", duration: "3.2s", started: "12 May 2025 09:30", actions: 8 },
  { id: "EXE-1247", name: "Payment Confirmation", trigger: "Webhook", status: "Success", duration: "1.4s", started: "12 May 2025 09:28", actions: 5 },
  { id: "EXE-1246", name: "Document Expiry Alert", trigger: "Schedule", status: "Success", duration: "7.1s", started: "12 May 2025 09:00", actions: 12 },
  { id: "EXE-1245", name: "Grievance Escalation", trigger: "Event", status: "Failed", duration: "2.3s", started: "12 May 2025 08:45", actions: 3 },
  { id: "EXE-1244", name: "Welcome Notification", trigger: "AI Assistant", status: "Success", duration: "0.9s", started: "12 May 2025 08:30", actions: 2 },
];

const users = [
  { id: 1, name: "Priya Sharma", email: "priya@gov.in", role: "Admin", status: "Active", last: "12 May 2025" },
  { id: 2, name: "Rahul Verma", email: "rahul@gov.in", role: "User", status: "Active", last: "11 May 2025" },
  { id: 3, name: "Anita Nair", email: "anita@gov.in", role: "Admin", status: "Active", last: "12 May 2025" },
  { id: 4, name: "John Doe", email: "john@gov.in", role: "User", status: "Inactive", last: "5 May 2025" },
  { id: 5, name: "Sanjay Kumar", email: "sanjay@gov.in", role: "User", status: "Active", last: "10 May 2025" },
];

const topAutomations = [
  { name: "Application Verification", success: 98.4 },
  { name: "Document Expiry Alert", success: 97.2 },
  { name: "Payment Confirmation", success: 99.0 },
  { name: "Grievance Escalation", success: 95.8 },
  { name: "Welcome Notification", success: 99.1 },
];

// ─── Shared UI ────────────────────────────────────────────────────────────────
function cn(...cls: (string | undefined | false | null)[]) {
  return cls.filter(Boolean).join(" ");
}

function Btn({
  children, variant = "primary", size = "sm", onClick, className = "", icon, disabled = false,
}: {
  children?: React.ReactNode; variant?: "primary" | "secondary" | "ghost" | "danger" | "outline";
  size?: "xs" | "sm" | "md"; onClick?: () => void; className?: string; icon?: React.ReactNode; disabled?: boolean;
}) {
  const base = "inline-flex items-center gap-1.5 font-medium rounded-lg transition-all focus:outline-none focus:ring-2 focus:ring-offset-1 cursor-pointer";
  const sizes = { xs: "px-2.5 py-1 text-xs", sm: "px-3 py-1.5 text-sm", md: "px-4 py-2 text-sm" };
  const variants = {
    primary: "bg-blue-600 text-white hover:bg-blue-700 focus:ring-blue-500",
    secondary: "bg-blue-50 text-blue-700 hover:bg-blue-100 focus:ring-blue-400",
    ghost: "text-slate-600 hover:bg-slate-100 hover:text-slate-900 focus:ring-slate-300",
    danger: "bg-red-600 text-white hover:bg-red-700 focus:ring-red-500",
    outline: "border border-slate-200 text-slate-700 hover:bg-slate-50 focus:ring-slate-300",
  };
  return (
    <button disabled={disabled} onClick={onClick}
      className={cn(base, sizes[size], variants[variant], disabled && "opacity-50 cursor-not-allowed", className)}>
      {icon && <span className="shrink-0">{icon}</span>}
      {children}
    </button>
  );
}

function Badge({ children, variant = "default" }: { children: React.ReactNode; variant?: "default" | "success" | "warning" | "error" | "purple" | "blue" }) {
  const v = {
    default: "bg-slate-100 text-slate-600",
    success: "bg-green-50 text-green-700",
    warning: "bg-amber-50 text-amber-700",
    error: "bg-red-50 text-red-700",
    purple: "bg-purple-50 text-purple-700",
    blue: "bg-blue-50 text-blue-700",
  };
  return <span className={cn("inline-flex items-center px-2 py-0.5 rounded-md text-xs font-medium", v[variant])}>{children}</span>;
}

function StatusBadge({ status }: { status: string }) {
  const map: Record<string, { variant: "success" | "error" | "warning" | "default" | "blue"; label: string }> = {
    Active: { variant: "success", label: "Active" },
    Inactive: { variant: "default", label: "Inactive" },
    Success: { variant: "success", label: "Success" },
    Failed: { variant: "error", label: "Failed" },
    Pending: { variant: "warning", label: "Pending" },
    "In Progress": { variant: "blue", label: "In Progress" },
    Connected: { variant: "success", label: "Connected" },
    Disconnected: { variant: "default", label: "Disconnected" },
    Error: { variant: "error", label: "Error" },
    Syncing: { variant: "blue", label: "Syncing" },
    Published: { variant: "success", label: "Published" },
    Draft: { variant: "default", label: "Draft" },
  };
  const m = map[status] || { variant: "default" as const, label: status };
  return <Badge variant={m.variant}>{m.label}</Badge>;
}

function Card({ children, className = "" }: { children: React.ReactNode; className?: string }) {
  return <div className={cn("bg-white rounded-xl border border-slate-200 shadow-sm", className)}>{children}</div>;
}

function SectionHeader({ title, subtitle, action }: { title: string; subtitle?: string; action?: React.ReactNode }) {
  return (
    <div className="flex items-start justify-between mb-6">
      <div>
        <h1 className="text-xl font-semibold text-slate-900">{title}</h1>
        {subtitle && <p className="text-sm text-slate-500 mt-0.5">{subtitle}</p>}
      </div>
      {action && <div className="flex items-center gap-2">{action}</div>}
    </div>
  );
}

function Input({ placeholder, value, onChange, icon, className = "" }: {
  placeholder?: string; value?: string; onChange?: (v: string) => void; icon?: React.ReactNode; className?: string;
}) {
  return (
    <div className={cn("relative", className)}>
      {icon && <span className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">{icon}</span>}
      <input
        value={value} placeholder={placeholder}
        onChange={e => onChange?.(e.target.value)}
        className={cn(
          "w-full border border-slate-200 rounded-lg text-sm text-slate-900 placeholder:text-slate-400 bg-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent",
          icon ? "pl-9 pr-3 py-2" : "px-3 py-2"
        )}
      />
    </div>
  );
}

function Select({ options, value, onChange, className = "" }: {
  options: { value: string; label: string }[]; value?: string; onChange?: (v: string) => void; className?: string;
}) {
  return (
    <select value={value} onChange={e => onChange?.(e.target.value)}
      className={cn("border border-slate-200 rounded-lg text-sm text-slate-700 bg-white focus:outline-none focus:ring-2 focus:ring-blue-500 px-3 py-2", className)}>
      {options.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
    </select>
  );
}

function StatCard({ label, value, delta, deltaUp, icon, color = "blue" }: {
  label: string; value: string; delta?: string; deltaUp?: boolean; icon: React.ReactNode; color?: "blue" | "purple" | "green" | "amber" | "red";
}) {
  const colors = {
    blue: "bg-blue-50 text-blue-600", purple: "bg-purple-50 text-purple-600",
    green: "bg-green-50 text-green-600", amber: "bg-amber-50 text-amber-600", red: "bg-red-50 text-red-600",
  };
  return (
    <Card className="p-4">
      <div className="flex items-start justify-between">
        <div>
          <p className="text-xs text-slate-500 font-medium uppercase tracking-wide">{label}</p>
          <p className="text-2xl font-bold text-slate-900 mt-1">{value}</p>
          {delta && (
            <div className={cn("flex items-center gap-1 mt-1 text-xs font-medium", deltaUp ? "text-green-600" : "text-red-600")}>
              {deltaUp ? <ArrowUp className="w-3 h-3" /> : <ArrowDown className="w-3 h-3" />}
              {delta} from last week
            </div>
          )}
        </div>
        <div className={cn("w-10 h-10 rounded-lg flex items-center justify-center", colors[color])}>
          <span className="w-5 h-5">{icon}</span>
        </div>
      </div>
    </Card>
  );
}

function Table({ columns, rows, onRowClick }: {
  columns: { key: string; label: string; width?: string }[];
  rows: Record<string, React.ReactNode>[];
  onRowClick?: (i: number) => void;
}) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-200 bg-slate-50">
            {columns.map(c => (
              <th key={c.key} className={cn("text-left px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wide", c.width)}>
                {c.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, i) => (
            <tr key={i}
              onClick={() => onRowClick?.(i)}
              className={cn("border-b border-slate-100 hover:bg-slate-50 transition-colors", onRowClick && "cursor-pointer")}>
              {columns.map(c => (
                <td key={c.key} className="px-4 py-3 text-slate-700">{row[c.key]}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Modal({ open, onClose, title, children, width = "max-w-lg" }: {
  open: boolean; onClose: () => void; title: string; children: React.ReactNode; width?: string;
}) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className={cn("relative bg-white rounded-2xl shadow-2xl w-full mx-4", width)}>
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-200">
          <h2 className="text-lg font-semibold text-slate-900">{title}</h2>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-600 p-1 rounded-lg hover:bg-slate-100">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="p-6">{children}</div>
      </div>
    </div>
  );
}

function DrawerPanel({ open, onClose, title, subtitle, children, width = "w-[480px]" }: {
  open: boolean; onClose: () => void; title: string; subtitle?: string; children: React.ReactNode; width?: string;
}) {
  return (
    <div className={cn("fixed inset-0 z-40 flex justify-end transition-all", open ? "pointer-events-auto" : "pointer-events-none")}>
      <div className={cn("absolute inset-0 bg-black/30 transition-opacity duration-300", open ? "opacity-100" : "opacity-0")} onClick={onClose} />
      <div className={cn("relative bg-white h-full shadow-2xl flex flex-col transition-transform duration-300", width, open ? "translate-x-0" : "translate-x-full")}>
        <div className="flex items-start justify-between px-6 py-5 border-b border-slate-200">
          <div>
            <h2 className="text-lg font-semibold text-slate-900">{title}</h2>
            {subtitle && <p className="text-sm text-slate-500 mt-0.5">{subtitle}</p>}
          </div>
          <button onClick={onClose} className="text-slate-400 hover:text-slate-600 p-1 rounded-lg hover:bg-slate-100 mt-0.5">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="flex-1 overflow-y-auto">{children}</div>
      </div>
    </div>
  );
}

function FormField({ label, children, hint }: { label: string; children: React.ReactNode; hint?: string }) {
  return (
    <div>
      <label className="block text-sm font-medium text-slate-700 mb-1.5">{label}</label>
      {children}
      {hint && <p className="text-xs text-slate-400 mt-1">{hint}</p>}
    </div>
  );
}

function Tabs({ tabs, active, onChange }: { tabs: { id: string; label: string }[]; active: string; onChange: (id: string) => void }) {
  return (
    <div className="flex border-b border-slate-200 gap-1">
      {tabs.map(t => (
        <button key={t.id} onClick={() => onChange(t.id)}
          className={cn(
            "px-4 py-2.5 text-sm font-medium transition-colors border-b-2 -mb-px",
            active === t.id
              ? "border-blue-600 text-blue-600"
              : "border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300"
          )}>
          {t.label}
        </button>
      ))}
    </div>
  );
}

// ─── Sidebar ─────────────────────────────────────────────────────────────────
const NAV_MAIN = [
  { id: "dashboard", label: "Dashboard", icon: LayoutDashboard },
  { id: "aiAssistants", label: "AI Assistants", icon: Bot },
  { id: "automations", label: "Automations", icon: Zap },
  { id: "knowledge", label: "Knowledge", icon: BookOpen },
  { id: "integrations", label: "Integrations", icon: Plug },
  { id: "toolsApis", label: "Tools & APIs", icon: Wrench },
  { id: "testPlayground", label: "Test & Playground", icon: FlaskConical },
  { id: "publish", label: "Publish", icon: Rocket },
  { id: "monitor", label: "Monitor", icon: Activity },
];
const NAV_MANAGE = [
  { id: "usersRoles", label: "Users & Roles", icon: Users },
];
const NAV_SYSTEM = [
  { id: "settings", label: "Settings", icon: Settings },
];

function Sidebar({ screen, role, onNavigate, collapsed, onToggle }: {
  screen: Screen; role: Role; onNavigate: (s: Screen) => void; collapsed: boolean; onToggle: () => void;
}) {
  const roleLabel = role === "superAdmin" ? "Super Admin" : role === "admin" ? "Admin" : "User";

  function NavItem({ item }: { item: { id: string; label: string; icon: React.ComponentType<{ className?: string }> } }) {
    const active = screen === item.id;
    const Icon = item.icon;
    return (
      <button onClick={() => onNavigate(item.id as Screen)}
        className={cn(
          "w-full flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium transition-all",
          active ? "bg-blue-50 text-blue-700" : "text-slate-600 hover:bg-slate-100 hover:text-slate-900",
          collapsed && "justify-center px-2"
        )}
        title={collapsed ? item.label : undefined}>
        <Icon className={cn("shrink-0", collapsed ? "w-5 h-5" : "w-4 h-4")} />
        {!collapsed && item.label}
      </button>
    );
  }

  return (
    <div className={cn(
      "flex flex-col h-full bg-white border-r border-slate-200 transition-all duration-200 shrink-0",
      collapsed ? "w-16" : "w-56"
    )}>
      {/* Logo */}
      <div className={cn("flex items-center gap-2 px-4 py-4 border-b border-slate-200", collapsed && "justify-center px-2")}>
        <div className="w-8 h-8 rounded-lg bg-blue-600 flex items-center justify-center shrink-0">
          <Sparkles className="w-4 h-4 text-white" />
        </div>
        {!collapsed && <span className="text-base font-bold text-slate-900 tracking-tight">R2WAI</span>}
      </div>

      {/* Navigation */}
      <div className="flex-1 overflow-y-auto py-3 px-2 space-y-0.5">
        {!collapsed && <p className="px-3 py-1.5 text-xs font-semibold text-slate-400 uppercase tracking-widest">Main</p>}
        {NAV_MAIN.filter(n => {
          if (role === "user") return ["dashboard", "aiAssistants", "automations", "knowledge", "testPlayground"].includes(n.id);
          return true;
        }).map(item => <NavItem key={item.id} item={item} />)}

        {role !== "user" && (
          <>
            {!collapsed && <p className="px-3 py-1.5 mt-3 text-xs font-semibold text-slate-400 uppercase tracking-widest">Manage</p>}
            {collapsed && <div className="my-2 border-t border-slate-200" />}
            {NAV_MANAGE.map(item => <NavItem key={item.id} item={item} />)}
          </>
        )}

        {!collapsed && <p className="px-3 py-1.5 mt-3 text-xs font-semibold text-slate-400 uppercase tracking-widest">System</p>}
        {collapsed && <div className="my-2 border-t border-slate-200" />}
        {role === "superAdmin" && [
          { id: "usersRoles", label: "AI Models", icon: Cpu },
          { id: "settings", label: "Security", icon: Shield },
        ].map(item => !collapsed ? null : <NavItem key={item.id} item={item} />)}
        {NAV_SYSTEM.map(item => <NavItem key={item.id} item={item} />)}
      </div>

      {/* Collapse button */}
      <div className="border-t border-slate-200 p-2">
        <button onClick={onToggle}
          className="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-xs text-slate-500 hover:bg-slate-100 transition-colors">
          {collapsed ? <ChevronRight className="w-4 h-4 mx-auto" /> : <><ChevronLeft className="w-4 h-4" /> Collapse</>}
        </button>
      </div>
    </div>
  );
}

// ─── TopBar ───────────────────────────────────────────────────────────────────
function TopBar({ role, onRoleChange, onNavigate }: {
  role: Role; onRoleChange: (r: Role) => void; onNavigate: (s: Screen) => void;
}) {
  const [notifOpen, setNotifOpen] = useState(false);
  const [userOpen, setUserOpen] = useState(false);
  const roleLabels = { superAdmin: "Super Admin", admin: "Admin", user: "User" };

  return (
    <div className="h-14 bg-white border-b border-slate-200 flex items-center px-4 gap-3 shrink-0">
      <div className="flex-1 max-w-md">
        <Input placeholder="Search anything…" icon={<Search className="w-4 h-4" />} />
      </div>
      <div className="flex items-center gap-2 ml-auto">
        {/* Role switcher (demo) */}
        <div className="flex items-center gap-1 bg-slate-50 border border-slate-200 rounded-lg p-1">
          {(["superAdmin", "admin", "user"] as Role[]).map(r => (
            <button key={r} onClick={() => onRoleChange(r)}
              className={cn("px-2.5 py-1 rounded-md text-xs font-medium transition-colors",
                role === r ? "bg-blue-600 text-white" : "text-slate-500 hover:text-slate-700")}>
              {r === "superAdmin" ? "Super" : r === "admin" ? "Admin" : "User"}
            </button>
          ))}
        </div>

        {/* Notifications */}
        <div className="relative">
          <button onClick={() => setNotifOpen(!notifOpen)}
            className="relative w-9 h-9 flex items-center justify-center rounded-lg hover:bg-slate-100 text-slate-500">
            <Bell className="w-4 h-4" />
            <span className="absolute top-2 right-2 w-2 h-2 bg-red-500 rounded-full" />
          </button>
          {notifOpen && (
            <div className="absolute right-0 mt-1 w-80 bg-white border border-slate-200 rounded-xl shadow-xl z-50 p-1">
              <div className="px-3 py-2 text-xs font-semibold text-slate-500 border-b border-slate-100">Notifications</div>
              {[
                { msg: 'Automation "Payment Confirmation" succeeded', time: "10 min ago", type: "success" },
                { msg: "New assistant \"Citizen Support\" created", time: "25 min ago", type: "info" },
                { msg: 'Automation "Document Expiry Alert" failed', time: "2 hrs ago", type: "error" },
              ].map((n, i) => (
                <div key={i} className="px-3 py-2.5 hover:bg-slate-50 rounded-lg cursor-pointer">
                  <p className="text-sm text-slate-700">{n.msg}</p>
                  <p className="text-xs text-slate-400 mt-0.5">{n.time}</p>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* User */}
        <div className="relative">
          <button onClick={() => setUserOpen(!userOpen)}
            className="flex items-center gap-2 pl-2 pr-3 py-1.5 rounded-lg hover:bg-slate-100 transition-colors">
            <div className="w-7 h-7 rounded-full bg-blue-600 text-white text-xs font-semibold flex items-center justify-center">SA</div>
            <div className="text-left hidden sm:block">
              <p className="text-sm font-medium text-slate-900 leading-none">Super Admin</p>
              <p className="text-xs text-slate-500 mt-0.5">{roleLabels[role]}</p>
            </div>
            <ChevronDown className="w-3.5 h-3.5 text-slate-400" />
          </button>
          {userOpen && (
            <div className="absolute right-0 mt-1 w-48 bg-white border border-slate-200 rounded-xl shadow-xl z-50 p-1">
              <button onClick={() => { onNavigate("settings"); setUserOpen(false); }}
                className="w-full flex items-center gap-2 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 rounded-lg">
                <User className="w-4 h-4" /> Profile
              </button>
              <button onClick={() => { onNavigate("settings"); setUserOpen(false); }}
                className="w-full flex items-center gap-2 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 rounded-lg">
                <Settings className="w-4 h-4" /> Settings
              </button>
              <div className="border-t border-slate-100 my-1" />
              <button className="w-full flex items-center gap-2 px-3 py-2 text-sm text-red-600 hover:bg-red-50 rounded-lg">
                <LogOut className="w-4 h-4" /> Sign Out
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ─── SCREEN: Login ────────────────────────────────────────────────────────────
function LoginScreen({ onLogin }: { onLogin: (role: Role) => void }) {
  const [email, setEmail] = useState("admin@gov.in");
  const [pass, setPass] = useState("••••••••");
  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 to-blue-50 flex items-center justify-center p-4">
      <div className="w-full max-w-sm">
        <div className="text-center mb-8">
          <div className="w-14 h-14 bg-blue-600 rounded-2xl flex items-center justify-center mx-auto mb-4 shadow-lg shadow-blue-200">
            <Sparkles className="w-7 h-7 text-white" />
          </div>
          <h1 className="text-2xl font-bold text-slate-900">R2WAI</h1>
          <p className="text-sm text-slate-500 mt-1">AI Assistant & Automation Platform</p>
        </div>
        <Card className="p-6">
          <h2 className="text-lg font-semibold text-slate-900 mb-1">Sign in to your account</h2>
          <p className="text-sm text-slate-500 mb-6">Enter your credentials to continue</p>
          <div className="space-y-4">
            <FormField label="Email address">
              <Input value={email} onChange={setEmail} placeholder="you@gov.in" />
            </FormField>
            <FormField label="Password">
              <div className="relative">
                <input type="password" value={pass} onChange={e => setPass(e.target.value)}
                  className="w-full border border-slate-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
                <button className="absolute right-3 top-1/2 -translate-y-1/2 text-xs text-blue-600 font-medium">Forgot?</button>
              </div>
            </FormField>
            <div className="flex items-center gap-2">
              <input type="checkbox" id="rem" className="rounded" />
              <label htmlFor="rem" className="text-sm text-slate-600">Remember me</label>
            </div>
          </div>
          <div className="mt-5 space-y-3">
            <Btn variant="primary" size="md" className="w-full justify-center" onClick={() => onLogin("superAdmin")}>
              Sign In
            </Btn>
            <div className="flex items-center gap-3">
              <div className="flex-1 h-px bg-slate-200" />
              <span className="text-xs text-slate-400">or continue with</span>
              <div className="flex-1 h-px bg-slate-200" />
            </div>
            <button className="w-full border border-slate-200 rounded-lg py-2 text-sm text-slate-600 hover:bg-slate-50 flex items-center justify-center gap-2 transition-colors">
              <div className="w-4 h-4 bg-blue-600 rounded text-white text-[10px] font-bold flex items-center justify-center">M</div>
              Sign in with SSO
            </button>
          </div>
          {/* Role quick-select for demo */}
          <div className="mt-4 pt-4 border-t border-slate-100">
            <p className="text-xs text-slate-400 text-center mb-2">Demo: Sign in as</p>
            <div className="flex gap-2">
              {([["superAdmin", "Super Admin"], ["admin", "Admin"], ["user", "User"]] as [Role, string][]).map(([r, l]) => (
                <button key={r} onClick={() => onLogin(r)}
                  className="flex-1 py-1.5 text-xs border border-slate-200 rounded-lg hover:bg-blue-50 hover:text-blue-700 hover:border-blue-200 text-slate-600 transition-colors font-medium">
                  {l}
                </button>
              ))}
            </div>
          </div>
        </Card>
        <p className="text-center text-xs text-slate-400 mt-6">© 2025 R2WAI. All rights reserved.</p>
      </div>
    </div>
  );
}

// ─── SCREEN: Super Admin Dashboard ───────────────────────────────────────────
function SuperAdminDashboard({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-slate-900">Welcome back, Super Admin! 👋</h1>
          <p className="text-sm text-slate-500 mt-0.5">Overview of your AI assistants, automations and system health.</p>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-sm text-slate-500">12 May 2025 – 18 May 2025</span>
          <Btn variant="primary" size="sm" icon={<Plus className="w-3.5 h-3.5" />}>New</Btn>
        </div>
      </div>

      {/* Stat cards */}
      <div className="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-6 gap-4">
        <StatCard label="AI Assistants" value="24" delta="12%" deltaUp icon={<Bot className="w-5 h-5" />} color="blue" />
        <StatCard label="Automations" value="56" delta="18%" deltaUp icon={<Zap className="w-5 h-5" />} color="purple" />
        <StatCard label="Executions" value="1,248" delta="22%" deltaUp icon={<Activity className="w-5 h-5" />} color="blue" />
        <StatCard label="Success Rate" value="98.6%" delta="1.4%" deltaUp icon={<TrendingUp className="w-5 h-5" />} color="green" />
        <StatCard label="Active Users" value="356" delta="15%" deltaUp icon={<Users className="w-5 h-5" />} color="amber" />
        <StatCard label="Failed Execs" value="18" delta="6%" deltaUp={false} icon={<XCircle className="w-5 h-5" />} color="red" />
      </div>

      {/* Charts row */}
      <div className="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <Card className="xl:col-span-2 p-5">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-sm font-semibold text-slate-900">Executions Overview</h3>
            <Select options={[{ value: "daily", label: "Daily" }, { value: "weekly", label: "Weekly" }]} value="daily" />
          </div>
          <ResponsiveContainer width="100%" height={180}>
            <AreaChart data={execData}>
              <defs>
                <linearGradient id="blueGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#2563EB" stopOpacity={0.15} />
                  <stop offset="95%" stopColor="#2563EB" stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="#F1F5F9" />
              <XAxis dataKey="date" tick={{ fontSize: 11, fill: "#94A3B8" }} axisLine={false} tickLine={false} />
              <YAxis tick={{ fontSize: 11, fill: "#94A3B8" }} axisLine={false} tickLine={false} />
              <Tooltip contentStyle={{ borderRadius: 8, border: "1px solid #E2E8F0", fontSize: 12 }} />
              <Area type="monotone" dataKey="execs" stroke="#2563EB" strokeWidth={2} fill="url(#blueGrad)" />
            </AreaChart>
          </ResponsiveContainer>
        </Card>

        <Card className="p-5">
          <h3 className="text-sm font-semibold text-slate-900 mb-4">Executions by Status</h3>
          <div className="flex items-center justify-center">
            <div className="relative">
              <ResponsiveContainer width={140} height={140}>
                <PieChart>
                  <Pie data={statusData} cx="50%" cy="50%" innerRadius={45} outerRadius={65} dataKey="value" strokeWidth={2}>
                    {statusData.map((d, i) => <Cell key={i} fill={d.color} />)}
                  </Pie>
                </PieChart>
              </ResponsiveContainer>
              <div className="absolute inset-0 flex flex-col items-center justify-center">
                <span className="text-xl font-bold text-slate-900">1,248</span>
                <span className="text-xs text-slate-400">Total</span>
              </div>
            </div>
          </div>
          <div className="space-y-2 mt-3">
            {statusData.map(d => (
              <div key={d.name} className="flex items-center justify-between text-xs">
                <div className="flex items-center gap-1.5">
                  <div className="w-2 h-2 rounded-full" style={{ background: d.color }} />
                  <span className="text-slate-600">{d.name}</span>
                </div>
                <span className="font-medium text-slate-900">{d.value} ({((d.value / 1248) * 100).toFixed(1)}%)</span>
              </div>
            ))}
          </div>
        </Card>
      </div>

      {/* Bottom row */}
      <div className="grid grid-cols-1 xl:grid-cols-3 gap-4">
        {/* Top Automations */}
        <Card className="p-5">
          <div className="flex items-center justify-between mb-3">
            <h3 className="text-sm font-semibold text-slate-900">Top Automations</h3>
            <button onClick={() => onNavigate("automations")} className="text-xs text-blue-600 hover:underline">View all</button>
          </div>
          <div className="space-y-3">
            {topAutomations.map(a => (
              <div key={a.name}>
                <div className="flex items-center justify-between text-xs mb-1">
                  <span className="text-slate-700 font-medium truncate mr-2">{a.name}</span>
                  <span className="text-slate-500 shrink-0">{a.success}%</span>
                </div>
                <div className="h-1.5 bg-slate-100 rounded-full">
                  <div className="h-full bg-green-500 rounded-full" style={{ width: `${a.success}%` }} />
                </div>
              </div>
            ))}
          </div>
        </Card>

        {/* Recent Activity */}
        <Card className="p-5">
          <div className="flex items-center justify-between mb-3">
            <h3 className="text-sm font-semibold text-slate-900">Recent Activity</h3>
            <button className="text-xs text-blue-600 hover:underline">View all</button>
          </div>
          <div className="space-y-3">
            {[
              { msg: 'Automation "Payment Confirmation" executed', time: "10 min ago", type: "success" },
              { msg: "New assistant \"Citizen Support\" created", time: "25 min ago", type: "info" },
              { msg: "Integration \"SMS Gateway\" connected", time: "1 hour ago", type: "success" },
              { msg: 'Automation "Document Expiry Alert" failed', time: "2 hrs ago", type: "error" },
              { msg: "User \"John Doe\" added by Admin", time: "3 hrs ago", type: "info" },
            ].map((a, i) => (
              <div key={i} className="flex items-start gap-2.5">
                <div className={cn("w-1.5 h-1.5 rounded-full mt-1.5 shrink-0",
                  a.type === "success" ? "bg-green-500" : a.type === "error" ? "bg-red-500" : "bg-blue-500")} />
                <div className="flex-1 min-w-0">
                  <p className="text-xs text-slate-700 leading-relaxed">{a.msg}</p>
                  <p className="text-xs text-slate-400 mt-0.5">{a.time}</p>
                </div>
              </div>
            ))}
          </div>
        </Card>

        {/* System Health + Quick Actions */}
        <div className="space-y-4">
          <Card className="p-5">
            <h3 className="text-sm font-semibold text-slate-900 mb-3">System Health</h3>
            <div className="space-y-2">
              {[
                { name: "API Services", status: "Healthy" },
                { name: "Database", status: "Healthy" },
                { name: "AI Service", status: "Healthy" },
                { name: "Storage", status: "Healthy" },
                { name: "Queue Service", status: "Healthy" },
              ].map(s => (
                <div key={s.name} className="flex items-center justify-between">
                  <span className="text-xs text-slate-600">{s.name}</span>
                  <div className="flex items-center gap-1.5">
                    <div className="w-1.5 h-1.5 rounded-full bg-green-500" />
                    <span className="text-xs text-green-600 font-medium">{s.status}</span>
                  </div>
                </div>
              ))}
            </div>
          </Card>
          <Card className="p-5">
            <h3 className="text-sm font-semibold text-slate-900 mb-3">Quick Actions</h3>
            <div className="grid grid-cols-2 gap-2">
              {[
                { label: "New Assistant", icon: Bot, screen: "aiAssistants" as Screen },
                { label: "New Automation", icon: Zap, screen: "automations" as Screen },
                { label: "Add Knowledge", icon: BookOpen, screen: "knowledge" as Screen },
                { label: "Add Integration", icon: Plug, screen: "integrations" as Screen },
                { label: "Test Playground", icon: FlaskConical, screen: "testPlayground" as Screen },
                { label: "View Monitor", icon: Activity, screen: "monitor" as Screen },
              ].map(a => (
                <button key={a.label} onClick={() => onNavigate(a.screen)}
                  className="flex items-center gap-1.5 p-2 rounded-lg border border-slate-200 hover:bg-blue-50 hover:border-blue-200 transition-colors text-left">
                  <a.icon className="w-3.5 h-3.5 text-blue-600 shrink-0" />
                  <span className="text-xs text-slate-700 font-medium">{a.label}</span>
                </button>
              ))}
            </div>
          </Card>
        </div>
      </div>
    </div>
  );
}

// ─── SCREEN: Admin Dashboard ───────────────────────────────────────────────
function AdminDashboard({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  return (
    <div className="p-6 space-y-6">
      <div>
        <h1 className="text-xl font-semibold text-slate-900">Good morning, Admin 👋</h1>
        <p className="text-sm text-slate-500 mt-0.5">Here's what's happening across your platform today.</p>
      </div>
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <StatCard label="AI Assistants" value="12" delta="8%" deltaUp icon={<Bot className="w-5 h-5" />} color="blue" />
        <StatCard label="Automations" value="28" delta="14%" deltaUp icon={<Zap className="w-5 h-5" />} color="purple" />
        <StatCard label="Today's Runs" value="342" delta="5%" deltaUp icon={<Activity className="w-5 h-5" />} color="green" />
        <StatCard label="Pending Review" value="3" icon={<AlertCircle className="w-5 h-5" />} color="amber" />
      </div>
      <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
        <Card className="p-5">
          <h3 className="text-sm font-semibold text-slate-900 mb-4">Weekly Automation Runs</h3>
          <ResponsiveContainer width="100%" height={160}>
            <BarChart data={weeklyData}>
              <CartesianGrid strokeDasharray="3 3" stroke="#F1F5F9" />
              <XAxis dataKey="day" tick={{ fontSize: 11, fill: "#94A3B8" }} axisLine={false} tickLine={false} />
              <YAxis tick={{ fontSize: 11, fill: "#94A3B8" }} axisLine={false} tickLine={false} />
              <Tooltip contentStyle={{ borderRadius: 8, border: "1px solid #E2E8F0", fontSize: 12 }} />
              <Bar dataKey="runs" fill="#2563EB" radius={[4, 4, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </Card>
        <Card className="p-5">
          <h3 className="text-sm font-semibold text-slate-900 mb-3">Active Assistants</h3>
          <div className="space-y-2">
            {assistants.slice(0, 4).map(a => (
              <div key={a.id} className="flex items-center justify-between py-1.5 border-b border-slate-100 last:border-0">
                <div className="flex items-center gap-2">
                  <div className="w-7 h-7 rounded-lg bg-blue-50 flex items-center justify-center">
                    <Bot className="w-3.5 h-3.5 text-blue-600" />
                  </div>
                  <div>
                    <p className="text-sm font-medium text-slate-800">{a.name}</p>
                    <p className="text-xs text-slate-400">{a.runs} runs today</p>
                  </div>
                </div>
                <StatusBadge status={a.status} />
              </div>
            ))}
          </div>
        </Card>
      </div>
      <div className="grid grid-cols-3 gap-4">
        {[
          { label: "New Assistant", icon: Bot, screen: "aiAssistants" as Screen, desc: "Deploy a new AI assistant" },
          { label: "New Automation", icon: Zap, screen: "automationWizard" as Screen, desc: "Build an automation flow" },
          { label: "Monitor Executions", icon: Activity, screen: "monitor" as Screen, desc: "View real-time activity" },
        ].map(a => (
          <button key={a.label} onClick={() => onNavigate(a.screen)}
            className="p-4 rounded-xl border-2 border-dashed border-slate-200 hover:border-blue-300 hover:bg-blue-50 transition-all text-left group">
            <a.icon className="w-6 h-6 text-blue-600 mb-2 group-hover:scale-110 transition-transform" />
            <p className="text-sm font-semibold text-slate-900">{a.label}</p>
            <p className="text-xs text-slate-400 mt-0.5">{a.desc}</p>
          </button>
        ))}
      </div>
    </div>
  );
}

// ─── SCREEN: User Dashboard ───────────────────────────────────────────────────
function UserDashboard({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  return (
    <div className="p-6 space-y-6">
      <div>
        <h1 className="text-xl font-semibold text-slate-900">Hello, Rahul 👋</h1>
        <p className="text-sm text-slate-500 mt-0.5">Your AI assistants and automations are ready.</p>
      </div>
      <div className="grid grid-cols-3 gap-4">
        <StatCard label="My Assistants" value="4" icon={<Bot className="w-5 h-5" />} color="blue" />
        <StatCard label="Automations Used" value="8" icon={<Zap className="w-5 h-5" />} color="purple" />
        <StatCard label="Interactions Today" value="23" delta="12%" deltaUp icon={<MessageSquare className="w-5 h-5" />} color="green" />
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
        <Card className="p-5">
          <h3 className="text-sm font-semibold text-slate-900 mb-3">My AI Assistants</h3>
          <div className="space-y-2">
            {assistants.slice(0, 3).map(a => (
              <div key={a.id} className="flex items-center gap-3 p-3 rounded-lg hover:bg-slate-50 cursor-pointer border border-slate-100"
                onClick={() => onNavigate("aiPlayground")}>
                <div className="w-9 h-9 rounded-xl bg-gradient-to-br from-blue-500 to-purple-500 flex items-center justify-center shrink-0">
                  <Bot className="w-4 h-4 text-white" />
                </div>
                <div className="flex-1">
                  <p className="text-sm font-semibold text-slate-800">{a.name}</p>
                  <p className="text-xs text-slate-400">{a.desc}</p>
                </div>
                <Btn variant="secondary" size="xs" onClick={() => onNavigate("aiPlayground")}>
                  Chat
                </Btn>
              </div>
            ))}
          </div>
        </Card>
        <Card className="p-5">
          <h3 className="text-sm font-semibold text-slate-900 mb-3">My Activity</h3>
          <div className="space-y-3">
            {[
              { msg: "Chat with Citizen Support", time: "10 min ago", icon: MessageSquare },
              { msg: "Automation \"Application Verification\" triggered", time: "1 hr ago", icon: Zap },
              { msg: "Knowledge base accessed: Policy Handbook", time: "2 hrs ago", icon: BookOpen },
              { msg: "Chat with FAQ Assistant", time: "Yesterday", icon: MessageSquare },
            ].map((a, i) => (
              <div key={i} className="flex items-center gap-3 py-2 border-b border-slate-100 last:border-0">
                <div className="w-7 h-7 rounded-lg bg-slate-100 flex items-center justify-center">
                  <a.icon className="w-3.5 h-3.5 text-slate-500" />
                </div>
                <div className="flex-1">
                  <p className="text-xs text-slate-700">{a.msg}</p>
                  <p className="text-xs text-slate-400 mt-0.5">{a.time}</p>
                </div>
              </div>
            ))}
          </div>
        </Card>
      </div>
    </div>
  );
}

// ─── SCREEN: AI Assistants List ───────────────────────────────────────────────
function AIAssistantsList({ onNavigate, onOpenDrawer }: {
  onNavigate: (s: Screen) => void; onOpenDrawer: (d: Drawer) => void;
}) {
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("All Status");
  const [confirmDelete, setConfirmDelete] = useState<number | null>(null);

  const filtered = assistants.filter(a =>
    a.name.toLowerCase().includes(search.toLowerCase()) &&
    (statusFilter === "All Status" || a.status === statusFilter)
  );

  return (
    <div className="p-6">
      <SectionHeader
        title="AI Assistants"
        subtitle="Create and manage AI assistants."
        action={<Btn variant="primary" size="sm" icon={<Plus className="w-3.5 h-3.5" />} onClick={() => onOpenDrawer("aiEdit")}>New Assistant</Btn>}
      />
      <Card>
        <div className="p-4 border-b border-slate-200 flex items-center gap-3 flex-wrap">
          <Input className="w-60" placeholder="Search assistants…" value={search} onChange={setSearch} icon={<Search className="w-4 h-4" />} />
          <Select value={statusFilter} onChange={setStatusFilter}
            options={[{ value: "All Status", label: "All Status" }, { value: "Active", label: "Active" }, { value: "Inactive", label: "Inactive" }]} />
          <Select value="All Channels" options={[{ value: "All Channels", label: "All Channels" }, { value: "Web", label: "Web" }, { value: "Mobile", label: "Mobile" }]} />
          <div className="ml-auto flex items-center gap-2">
            <Btn variant="ghost" size="sm" icon={<RefreshCw className="w-3.5 h-3.5" />}>Refresh</Btn>
            <Btn variant="outline" size="sm" icon={<Download className="w-3.5 h-3.5" />}>Export</Btn>
          </div>
        </div>
        <Table
          columns={[
            { key: "name", label: "Name" },
            { key: "desc", label: "Description" },
            { key: "actions_icons", label: "" },
            { key: "status", label: "Status" },
            { key: "updated", label: "Last Updated" },
            { key: "actions", label: "Actions" },
          ]}
          rows={filtered.map(a => ({
            name: (
              <div className="flex items-center gap-2.5">
                <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-blue-500 to-purple-600 flex items-center justify-center shrink-0">
                  <Bot className="w-4 h-4 text-white" />
                </div>
                <span className="font-medium text-slate-900">{a.name}</span>
              </div>
            ),
            desc: <span className="text-slate-500 text-xs">{a.desc}</span>,
            actions_icons: (
              <div className="flex items-center gap-1">
                <button className="p-1 rounded hover:bg-slate-100 text-slate-400 hover:text-blue-600" title="Playground" onClick={() => onNavigate("aiPlayground")}><Play className="w-3.5 h-3.5" /></button>
                <button className="p-1 rounded hover:bg-slate-100 text-slate-400 hover:text-blue-600" title="Edit" onClick={() => onOpenDrawer("aiEdit")}><Edit className="w-3.5 h-3.5" /></button>
                <button className="p-1 rounded hover:bg-slate-100 text-slate-400 hover:text-blue-600" title="View" onClick={() => onNavigate("aiDetail")}><Eye className="w-3.5 h-3.5" /></button>
              </div>
            ),
            status: <StatusBadge status={a.status} />,
            updated: <span className="text-xs text-slate-500">{a.updated}</span>,
            actions: (
              <div className="flex items-center gap-1.5">
                <Btn variant="secondary" size="xs" onClick={() => onNavigate("aiPlayground")}>Test</Btn>
                <button className="p-1.5 rounded hover:bg-red-50 text-slate-400 hover:text-red-600" onClick={() => setConfirmDelete(a.id)}>
                  <Trash2 className="w-3.5 h-3.5" />
                </button>
              </div>
            ),
          }))}
        />
        <div className="p-4 flex items-center justify-between border-t border-slate-200">
          <span className="text-xs text-slate-500">Showing {filtered.length} of {assistants.length} assistants</span>
          <div className="flex items-center gap-1">
            {[1, 2, 3, "...", 5].map((p, i) => (
              <button key={i} className={cn("w-7 h-7 text-xs rounded-md", p === 1 ? "bg-blue-600 text-white" : "text-slate-500 hover:bg-slate-100")}>{p}</button>
            ))}
          </div>
        </div>
      </Card>

      <Modal open={confirmDelete !== null} onClose={() => setConfirmDelete(null)} title="Delete Assistant">
        <div className="flex items-start gap-3 mb-5">
          <div className="w-10 h-10 rounded-full bg-red-100 flex items-center justify-center shrink-0">
            <AlertTriangle className="w-5 h-5 text-red-600" />
          </div>
          <div>
            <p className="text-sm font-medium text-slate-900">Are you sure you want to delete this assistant?</p>
            <p className="text-sm text-slate-500 mt-1">This action cannot be undone. All associated configurations will be permanently removed.</p>
          </div>
        </div>
        <div className="flex justify-end gap-2">
          <Btn variant="outline" size="sm" onClick={() => setConfirmDelete(null)}>Cancel</Btn>
          <Btn variant="danger" size="sm" onClick={() => setConfirmDelete(null)}>Delete Assistant</Btn>
        </div>
      </Modal>
    </div>
  );
}

// ─── SCREEN: AI Assistant Detail ──────────────────────────────────────────────
function AIAssistantDetail({ onNavigate, onOpenDrawer }: { onNavigate: (s: Screen) => void; onOpenDrawer: (d: Drawer) => void }) {
  const [tab, setTab] = useState("general");
  const tabs = [
    { id: "general", label: "General" },
    { id: "instructions", label: "Instructions" },
    { id: "knowledge", label: "Knowledge" },
    { id: "tools", label: "Tools" },
    { id: "behavior", label: "Behavior" },
    { id: "channels", label: "Channels" },
    { id: "advanced", label: "Advanced" },
  ];

  return (
    <div className="p-6 space-y-5">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <button onClick={() => onNavigate("aiAssistants")} className="text-slate-400 hover:text-slate-600 p-1 rounded hover:bg-slate-100">
            <ChevronLeft className="w-5 h-5" />
          </button>
          <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-500 to-purple-600 flex items-center justify-center">
            <Bot className="w-5 h-5 text-white" />
          </div>
          <div>
            <h1 className="text-xl font-semibold text-slate-900">Citizen Support</h1>
            <div className="flex items-center gap-2 mt-0.5">
              <StatusBadge status="Active" />
              <span className="text-xs text-slate-400">Web · Mobile</span>
            </div>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Btn variant="outline" size="sm" icon={<Play className="w-3.5 h-3.5" />} onClick={() => onNavigate("aiPlayground")}>Test</Btn>
          <Btn variant="secondary" size="sm" icon={<Edit className="w-3.5 h-3.5" />} onClick={() => onOpenDrawer("aiEdit")}>Edit</Btn>
          <Btn variant="primary" size="sm" icon={<Rocket className="w-3.5 h-3.5" />} onClick={() => onNavigate("publish")}>Publish</Btn>
        </div>
      </div>

      <div className="border-b border-slate-200">
        <Tabs tabs={tabs} active={tab} onChange={setTab} />
      </div>

      {tab === "general" && (
        <div className="grid grid-cols-1 xl:grid-cols-3 gap-5">
          <div className="xl:col-span-2 space-y-4">
            <Card className="p-5 space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <p className="text-xs text-slate-500 font-medium mb-1">Assistant Name</p>
                  <p className="text-sm font-medium text-slate-900">Citizen Support</p>
                </div>
                <div>
                  <p className="text-xs text-slate-500 font-medium mb-1">Status</p>
                  <StatusBadge status="Active" />
                </div>
                <div>
                  <p className="text-xs text-slate-500 font-medium mb-1">Default Model</p>
                  <p className="text-sm text-slate-900">R2WAI LLM (Local)</p>
                </div>
                <div>
                  <p className="text-xs text-slate-500 font-medium mb-1">Language</p>
                  <p className="text-sm text-slate-900">English</p>
                </div>
              </div>
              <div>
                <p className="text-xs text-slate-500 font-medium mb-1">Description</p>
                <p className="text-sm text-slate-700">Provides information and support to citizens on services and applications.</p>
              </div>
              <div>
                <p className="text-xs text-slate-500 font-medium mb-1">Welcome Message</p>
                <p className="text-sm text-slate-700 italic">"Hello! How can I help you today?"</p>
              </div>
              <div>
                <p className="text-xs text-slate-500 font-medium mb-1">Fallback Message</p>
                <p className="text-sm text-slate-700 italic">"Sorry, I didn't understand that."</p>
              </div>
            </Card>
          </div>
          <div className="space-y-4">
            <Card className="p-5">
              <h3 className="text-sm font-semibold text-slate-900 mb-3">Performance (7 days)</h3>
              <div className="space-y-3">
                {[
                  { label: "Total Interactions", value: "456" },
                  { label: "Avg Response Time", value: "1.2s" },
                  { label: "Resolution Rate", value: "87.3%" },
                  { label: "User Satisfaction", value: "4.6 / 5.0" },
                ].map(m => (
                  <div key={m.label} className="flex items-center justify-between">
                    <span className="text-xs text-slate-500">{m.label}</span>
                    <span className="text-sm font-semibold text-slate-900">{m.value}</span>
                  </div>
                ))}
              </div>
            </Card>
            <Card className="p-5">
              <h3 className="text-sm font-semibold text-slate-900 mb-3">Connected Avatar</h3>
              <div className="flex items-center gap-3">
                <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-blue-400 to-purple-500 flex items-center justify-center">
                  <Bot className="w-6 h-6 text-white" />
                </div>
                <div>
                  <p className="text-sm font-medium text-slate-900">AI Avatar</p>
                  <p className="text-xs text-slate-400">Default · Round</p>
                </div>
              </div>
            </Card>
          </div>
        </div>
      )}

      {tab === "instructions" && (
        <Card className="p-5">
          <h3 className="text-sm font-semibold text-slate-900 mb-3">System Prompt & Instructions</h3>
          <div className="bg-slate-50 rounded-lg p-4 font-mono text-xs text-slate-700 leading-relaxed">
            You are a helpful government assistant for citizens. Your role is to:<br />
            1. Provide accurate information about government services<br />
            2. Guide citizens through application processes<br />
            3. Answer queries about eligibility and documentation<br />
            4. Escalate complex issues to human officers<br /><br />
            Always be polite, clear, and concise. Do not provide legal advice.
          </div>
          <div className="mt-4 flex items-center gap-2">
            <Btn variant="secondary" size="sm">Edit Instructions</Btn>
            <Badge variant="blue">2,847 tokens</Badge>
          </div>
        </Card>
      )}

      {tab === "knowledge" && (
        <Card className="p-5">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-sm font-semibold text-slate-900">Connected Knowledge Bases</h3>
            <Btn variant="secondary" size="sm" icon={<Plus className="w-3.5 h-3.5" />}>Add Source</Btn>
          </div>
          <div className="space-y-2">
            {knowledgeBases.slice(0, 3).map(k => (
              <div key={k.id} className="flex items-center justify-between p-3 rounded-lg border border-slate-200">
                <div className="flex items-center gap-2.5">
                  <BookOpen className="w-4 h-4 text-blue-600" />
                  <div>
                    <p className="text-sm font-medium text-slate-900">{k.name}</p>
                    <p className="text-xs text-slate-400">{k.docs} documents · {k.type}</p>
                  </div>
                </div>
                <StatusBadge status={k.status} />
              </div>
            ))}
          </div>
        </Card>
      )}

      {(tab === "tools" || tab === "behavior" || tab === "channels") && (
        <Card className="p-8 text-center">
          <Sliders className="w-8 h-8 text-slate-300 mx-auto mb-2" />
          <p className="text-sm text-slate-500">Configuration for <strong>{tab}</strong> available in Edit mode.</p>
          <Btn variant="secondary" size="sm" className="mt-3" onClick={() => onOpenDrawer("aiEdit")}>Open Editor</Btn>
        </Card>
      )}

      {tab === "advanced" && (
        <Card className="p-5">
          <div className="flex items-center gap-2 mb-4">
            <Shield className="w-4 h-4 text-amber-600" />
            <h3 className="text-sm font-semibold text-slate-900">Advanced Configuration</h3>
            <Badge variant="warning">Developer Mode</Badge>
          </div>
          <div className="bg-slate-900 rounded-lg p-4 font-mono text-xs text-green-400 leading-relaxed">
            {`{
  "agent_type": "conversational",
  "process_engine": "v2.3",
  "intent_detection": "ml_enhanced",
  "decision_tree": "auto",
  "schema_version": "2025-Q2",
  "runtime_context": {
    "session_timeout": 1800,
    "max_turns": 50
  }
}`}
          </div>
        </Card>
      )}
    </div>
  );
}

// ─── DRAWER: AI Assistant Edit ─────────────────────────────────────────────────
function AIAssistantEditDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [name, setName] = useState("Citizen Support");
  const [desc, setDesc] = useState("Provides information and support to citizens on services and applications.");
  const [lang, setLang] = useState("en");
  const [activeStatus, setActiveStatus] = useState(true);

  return (
    <DrawerPanel open={open} onClose={onClose} title="Edit Assistant" subtitle="Citizen Support" width="w-[520px]">
      <div className="p-6 space-y-5">
        <FormField label="Assistant Name">
          <Input value={name} onChange={setName} />
        </FormField>
        <FormField label="Description" hint="Describe what this assistant does for users.">
          <textarea value={desc} onChange={e => setDesc(e.target.value)}
            rows={3} className="w-full border border-slate-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" />
        </FormField>
        <FormField label="Status">
          <div className="flex items-center gap-3">
            <button onClick={() => setActiveStatus(!activeStatus)}
              className={cn("w-10 h-6 rounded-full transition-colors flex items-center px-0.5",
                activeStatus ? "bg-blue-600" : "bg-slate-300")}>
              <div className={cn("w-4 h-4 bg-white rounded-full shadow transition-transform", activeStatus ? "translate-x-4" : "translate-x-0")} />
            </button>
            <span className="text-sm text-slate-700">{activeStatus ? "Active" : "Inactive"}</span>
          </div>
        </FormField>
        <FormField label="Default Model">
          <Select value="local" onChange={() => { }}
            options={[
              { value: "local", label: "R2WAI LLM (Local)" },
              { value: "cloud", label: "R2WAI LLM (Cloud)" },
              { value: "custom", label: "Custom Model" },
            ]} className="w-full" />
        </FormField>
        <FormField label="Language">
          <Select value={lang} onChange={setLang}
            options={[{ value: "en", label: "English" }, { value: "hi", label: "Hindi" }, { value: "ta", label: "Tamil" }]}
            className="w-full" />
        </FormField>
        <FormField label="Welcome Message">
          <Input value="Hello! How can I help you today?" />
        </FormField>
        <FormField label="Fallback Message">
          <Input value="Sorry, I didn't understand that." />
        </FormField>

        <div className="pt-2 border-t border-slate-200">
          <h4 className="text-sm font-semibold text-slate-900 mb-3">Channels</h4>
          {["Web Widget", "Mobile App", "WhatsApp", "Telegram"].map(c => (
            <label key={c} className="flex items-center gap-2.5 py-2 cursor-pointer">
              <input type="checkbox" defaultChecked={["Web Widget", "Mobile App"].includes(c)} className="rounded" />
              <span className="text-sm text-slate-700">{c}</span>
            </label>
          ))}
        </div>
      </div>
      <div className="sticky bottom-0 bg-white border-t border-slate-200 px-6 py-4 flex items-center justify-between gap-3">
        <Btn variant="ghost" size="sm" onClick={onClose}>Cancel</Btn>
        <div className="flex gap-2">
          <Btn variant="outline" size="sm">Save Draft</Btn>
          <Btn variant="primary" size="sm" icon={<Save className="w-3.5 h-3.5" />} onClick={onClose}>Save Changes</Btn>
        </div>
      </div>
    </DrawerPanel>
  );
}

// ─── SCREEN: AI Assistant Playground ─────────────────────────────────────────
function AIAssistantPlayground({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  const [selected, setSelected] = useState("Citizen Support");
  const [msg, setMsg] = useState("");
  const [chat, setChat] = useState([
    { role: "assistant", text: "Hello! How can I help you today?" },
    { role: "user", text: "What documents are required for income certificate?" },
    { role: "assistant", text: "You need the following documents:\n1. Aadhaar Card\n2. Address Proof\n3. Income Proof\n\nIs there anything else you need?" },
  ]);
  const [tab, setTab] = useState("assistant");
  const chatRef = useRef<HTMLDivElement>(null);

  function sendMsg() {
    if (!msg.trim()) return;
    setChat(c => [...c, { role: "user", text: msg }, { role: "assistant", text: "Let me help you with that. Could you provide more details?" }]);
    setMsg("");
  }

  useEffect(() => {
    chatRef.current?.scrollTo({ top: chatRef.current.scrollHeight, behavior: "smooth" });
  }, [chat]);

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <SectionHeader title="Test & Playground" subtitle="Test assistants, automations and APIs." />
        <Btn variant="ghost" size="sm" onClick={() => onNavigate("aiAssistants")}>
          <ChevronLeft className="w-4 h-4" /> Back
        </Btn>
      </div>

      <div className="flex gap-1 border-b border-slate-200 mb-4">
        {["assistant", "automation", "api"].map(t => (
          <button key={t} onClick={() => setTab(t)}
            className={cn("px-4 py-2 text-sm font-medium capitalize border-b-2 -mb-px transition-colors",
              tab === t ? "border-blue-600 text-blue-600" : "border-transparent text-slate-500 hover:text-slate-700")}>
            {t === "api" ? "API / Trigger" : t.charAt(0).toUpperCase() + t.slice(1)}
          </button>
        ))}
      </div>

      {tab === "assistant" && (
        <div className="flex gap-4" style={{ height: "calc(100vh - 260px)" }}>
          <Card className="flex-1 flex flex-col">
            <div className="p-4 border-b border-slate-200 flex items-center gap-3">
              <Select value={selected} onChange={setSelected}
                className="flex-1"
                options={assistants.map(a => ({ value: a.name, label: a.name }))} />
              <Btn variant="outline" size="sm">Clear Chat</Btn>
            </div>
            <div ref={chatRef} className="flex-1 overflow-y-auto p-4 space-y-4">
              {chat.map((m, i) => (
                <div key={i} className={cn("flex gap-3", m.role === "user" && "justify-end")}>
                  {m.role === "assistant" && (
                    <div className="w-7 h-7 rounded-full bg-blue-600 flex items-center justify-center shrink-0 mt-0.5">
                      <Bot className="w-3.5 h-3.5 text-white" />
                    </div>
                  )}
                  <div className={cn(
                    "max-w-xs rounded-2xl px-4 py-2.5 text-sm whitespace-pre-wrap",
                    m.role === "user"
                      ? "bg-blue-600 text-white rounded-tr-sm"
                      : "bg-slate-100 text-slate-800 rounded-tl-sm"
                  )}>{m.text}</div>
                </div>
              ))}
            </div>
            <div className="p-4 border-t border-slate-200">
              <div className="flex items-center gap-2">
                <input value={msg} onChange={e => setMsg(e.target.value)}
                  onKeyDown={e => e.key === "Enter" && sendMsg()}
                  placeholder="Type your message…"
                  className="flex-1 border border-slate-200 rounded-xl px-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
                <button onClick={sendMsg}
                  className="w-9 h-9 bg-blue-600 rounded-xl flex items-center justify-center hover:bg-blue-700 transition-colors">
                  <Send className="w-4 h-4 text-white" />
                </button>
              </div>
            </div>
          </Card>

          <Card className="w-72 p-4 space-y-4 overflow-y-auto">
            <h3 className="text-sm font-semibold text-slate-900">Execution Details</h3>
            {[
              { label: "Status", value: <Badge variant="success">Success</Badge> },
              { label: "Model", value: "R2WAI LLM" },
              { label: "Response Time", value: "1.42 sec" },
              { label: "Tokens", value: "258" },
            ].map(d => (
              <div key={d.label} className="flex items-center justify-between">
                <span className="text-xs text-slate-500">{d.label}</span>
                <span className="text-xs font-medium text-slate-800">{d.value}</span>
              </div>
            ))}
            <div className="border-t border-slate-100 pt-3">
              <p className="text-xs font-medium text-slate-700 mb-2">Steps</p>
              {["Input Received", "Intent Detected", "Knowledge Lookup", "Response Generated"].map((s, i) => (
                <div key={i} className="flex items-center gap-2 py-1.5">
                  <CheckCircle className="w-3.5 h-3.5 text-green-500 shrink-0" />
                  <span className="text-xs text-slate-600">{s}</span>
                </div>
              ))}
            </div>
          </Card>
        </div>
      )}

      {tab === "automation" && (
        <Card className="p-8 text-center">
          <Zap className="w-10 h-10 text-blue-300 mx-auto mb-3" />
          <h3 className="text-base font-semibold text-slate-900 mb-1">Test an Automation</h3>
          <p className="text-sm text-slate-500 mb-4">Select an automation and trigger it manually with test data.</p>
          <Select value="" options={automations.map(a => ({ value: a.name, label: a.name }))} className="mx-auto w-72 mb-3" />
          <Btn variant="primary" size="md" icon={<Play className="w-4 h-4" />}>Run Test</Btn>
        </Card>
      )}

      {tab === "api" && (
        <Card className="p-5">
          <h3 className="text-sm font-semibold text-slate-900 mb-4">API / Trigger Test</h3>
          <div className="space-y-4">
            <div className="flex items-center gap-2">
              <select className="border border-slate-200 rounded-lg px-3 py-2 text-sm bg-white text-slate-700">
                <option>POST</option><option>GET</option><option>PUT</option>
              </select>
              <input defaultValue="/api/v1/assistant/citizen-support/invoke" className="flex-1 border border-slate-200 rounded-lg px-3 py-2 text-sm font-mono" />
              <Btn variant="primary" size="sm">Send</Btn>
            </div>
            <div>
              <p className="text-xs font-medium text-slate-700 mb-1">Request Body</p>
              <div className="bg-slate-900 rounded-lg p-3 font-mono text-xs text-green-400">
                {`{\n  "message": "Hello, I need help",\n  "session_id": "test-001"\n}`}
              </div>
            </div>
            <div>
              <p className="text-xs font-medium text-slate-700 mb-1">Response</p>
              <div className="bg-slate-900 rounded-lg p-3 font-mono text-xs text-blue-300">
                {`{\n  "response": "Hello! How can I help you today?",\n  "session_id": "test-001",\n  "tokens": 24\n}`}
              </div>
            </div>
          </div>
        </Card>
      )}
    </div>
  );
}

// ─── SCREEN: Automations List ─────────────────────────────────────────────────
function AutomationsList({ onNavigate, onOpenDrawer }: {
  onNavigate: (s: Screen) => void; onOpenDrawer: (d: Drawer) => void;
}) {
  const [search, setSearch] = useState("");
  const [triggerFilter, setTriggerFilter] = useState("All Triggers");

  const filtered = automations.filter(a =>
    a.name.toLowerCase().includes(search.toLowerCase()) &&
    (triggerFilter === "All Triggers" || a.trigger === triggerFilter)
  );

  return (
    <div className="p-6">
      <SectionHeader
        title="Automations"
        subtitle="Create and manage automations."
        action={
          <div className="flex gap-2">
            <Btn variant="outline" size="sm" icon={<GitBranch className="w-3.5 h-3.5" />} onClick={() => onNavigate("workflowBuilder")}>
              Advanced Builder
            </Btn>
            <Btn variant="primary" size="sm" icon={<Plus className="w-3.5 h-3.5" />} onClick={() => onNavigate("automationWizard")}>
              New Automation
            </Btn>
          </div>
        }
      />
      <Card>
        <div className="p-4 border-b border-slate-200 flex items-center gap-3 flex-wrap">
          <Input className="w-60" placeholder="Search automations…" value={search} onChange={setSearch} icon={<Search className="w-4 h-4" />} />
          <Select value={triggerFilter} onChange={setTriggerFilter}
            options={[
              { value: "All Triggers", label: "All Triggers" },
              { value: "API", label: "API" }, { value: "Webhook", label: "Webhook" },
              { value: "Schedule", label: "Schedule" }, { value: "Event", label: "Event" },
              { value: "AI Assistant", label: "AI Assistant" },
            ]} />
          <Select value="All Status" options={[{ value: "All Status", label: "All Status" }, { value: "Active", label: "Active" }, { value: "Inactive", label: "Inactive" }]} />
          <div className="ml-auto flex gap-2">
            <Btn variant="ghost" size="sm" icon={<RefreshCw className="w-3.5 h-3.5" />}>Refresh</Btn>
          </div>
        </div>
        <Table
          columns={[
            { key: "name", label: "Name" },
            { key: "trigger", label: "Trigger" },
            { key: "does", label: "What it does" },
            { key: "status", label: "Status" },
            { key: "lastRun", label: "Last Run" },
            { key: "success", label: "Success Rate" },
            { key: "actions", label: "Actions" },
          ]}
          rows={filtered.map(a => ({
            name: <span className="font-medium text-slate-900">{a.name}</span>,
            trigger: (
              <div className="flex items-center gap-1.5">
                {a.trigger === "API" && <Globe className="w-3.5 h-3.5 text-blue-500" />}
                {a.trigger === "Schedule" && <Calendar className="w-3.5 h-3.5 text-purple-500" />}
                {a.trigger === "Webhook" && <Webhook className="w-3.5 h-3.5 text-green-500" />}
                {a.trigger === "Event" && <Radio className="w-3.5 h-3.5 text-amber-500" />}
                {a.trigger === "AI Assistant" && <Bot className="w-3.5 h-3.5 text-blue-500" />}
                <span className="text-xs text-slate-600">{a.trigger}</span>
              </div>
            ),
            does: <span className="text-xs text-slate-500 max-w-xs block truncate">{a.does}</span>,
            status: <StatusBadge status={a.status} />,
            lastRun: <span className="text-xs text-slate-500">{a.lastRun}</span>,
            success: (
              <div className="flex items-center gap-2">
                <div className="w-16 h-1.5 bg-slate-100 rounded-full">
                  <div className="h-full bg-green-500 rounded-full" style={{ width: `${a.success}%` }} />
                </div>
                <span className="text-xs font-medium text-slate-700">{a.success}%</span>
              </div>
            ),
            actions: (
              <div className="flex items-center gap-1">
                <button className="p-1.5 rounded hover:bg-slate-100 text-slate-400 hover:text-blue-600" title="View" onClick={() => onOpenDrawer("automationDetail")}>
                  <Eye className="w-3.5 h-3.5" />
                </button>
                <button className="p-1.5 rounded hover:bg-slate-100 text-slate-400 hover:text-blue-600" title="Edit" onClick={() => onNavigate("workflowBuilder")}>
                  <Edit className="w-3.5 h-3.5" />
                </button>
                <button className="p-1.5 rounded hover:bg-slate-100 text-slate-400 hover:text-blue-600" title="Run">
                  <Play className="w-3.5 h-3.5" />
                </button>
                <button className="p-1.5 rounded hover:bg-red-50 text-slate-400 hover:text-red-600" title="Delete">
                  <Trash2 className="w-3.5 h-3.5" />
                </button>
              </div>
            ),
          }))}
        />
        <div className="p-4 flex items-center justify-between border-t border-slate-200">
          <span className="text-xs text-slate-500">Showing {filtered.length} of {automations.length} automations</span>
          <div className="flex items-center gap-1">
            {[1, 2, 3].map(p => (
              <button key={p} className={cn("w-7 h-7 text-xs rounded-md", p === 1 ? "bg-blue-600 text-white" : "text-slate-500 hover:bg-slate-100")}>{p}</button>
            ))}
          </div>
        </div>
      </Card>
    </div>
  );
}

// ─── DRAWER: Automation Detail ──────────────────────────────────────────────
function AutomationDetailDrawer({ open, onClose, onNavigate }: {
  open: boolean; onClose: () => void; onNavigate: (s: Screen) => void;
}) {
  return (
    <DrawerPanel open={open} onClose={onClose} title="Automation Details" subtitle="Application Verification" width="w-[480px]">
      <div className="p-6 space-y-5">
        <div className="grid grid-cols-2 gap-4">
          {[
            { label: "Status", value: <StatusBadge status="Active" /> },
            { label: "Trigger", value: <Badge variant="blue">API</Badge> },
            { label: "Last Run", value: "10 min ago" },
            { label: "Success Rate", value: "98.4%" },
            { label: "Total Runs", value: "1,248" },
            { label: "Avg Duration", value: "3.2s" },
          ].map(d => (
            <div key={d.label}>
              <p className="text-xs text-slate-400 mb-1">{d.label}</p>
              <div className="text-sm font-medium text-slate-900">{d.value}</div>
            </div>
          ))}
        </div>
        <div>
          <p className="text-xs text-slate-400 mb-2">What it does</p>
          <p className="text-sm text-slate-700 bg-slate-50 rounded-lg p-3">Verify documents and notify applicant upon submission of new applications via the API endpoint.</p>
        </div>
        <div>
          <p className="text-xs text-slate-400 mb-2">Flow Steps</p>
          <div className="space-y-2">
            {[
              { label: "Trigger: API Received", status: "success" },
              { label: "AI: Extract Application Data", status: "success" },
              { label: "Condition: Documents Valid?", status: "success" },
              { label: "Action: Update Application Status", status: "success" },
              { label: "Notification: SMS to Applicant", status: "success" },
              { label: "Response: Confirmation Sent", status: "success" },
            ].map((s, i) => (
              <div key={i} className="flex items-center gap-2.5 p-2 rounded-lg bg-slate-50">
                <CheckCircle className="w-4 h-4 text-green-500 shrink-0" />
                <span className="text-xs text-slate-700">{s.label}</span>
              </div>
            ))}
          </div>
        </div>
        <div className="flex gap-2 pt-2">
          <Btn variant="secondary" size="sm" className="flex-1 justify-center" onClick={() => { onClose(); onNavigate("workflowBuilder"); }}>
            Edit in Builder
          </Btn>
          <Btn variant="primary" size="sm" className="flex-1 justify-center" icon={<Play className="w-3.5 h-3.5" />}>
            Run Now
          </Btn>
        </div>
      </div>
    </DrawerPanel>
  );
}

// ─── SCREEN: New Automation Wizard ────────────────────────────────────────────
function NewAutomationWizard({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  const [step, setStep] = useState(0);
  const steps = ["Trigger", "What should happen", "Review", "Test", "Publish"];
  const [trigger, setTrigger] = useState("");
  const [automName, setAutomName] = useState("");

  const triggers = [
    { id: "api", label: "API Call", desc: "Trigger when an API endpoint is called", icon: Globe, color: "blue" },
    { id: "webhook", label: "Webhook", desc: "Trigger on incoming webhook events", icon: Webhook, color: "green" },
    { id: "event", label: "Platform Event", desc: "Trigger on internal platform events", icon: Radio, color: "amber" },
    { id: "schedule", label: "Schedule", desc: "Run on a recurring schedule", icon: Calendar, color: "purple" },
    { id: "manual", label: "Manual", desc: "Trigger manually or by a user action", icon: Play, color: "blue" },
    { id: "assistant", label: "AI Assistant", desc: "Triggered from within an AI conversation", icon: Bot, color: "purple" },
  ];

  return (
    <div className="p-6 max-w-3xl mx-auto">
      <div className="flex items-center gap-3 mb-6">
        <button onClick={() => onNavigate("automations")} className="p-1 rounded hover:bg-slate-100 text-slate-400">
          <ChevronLeft className="w-5 h-5" />
        </button>
        <div>
          <h1 className="text-xl font-semibold text-slate-900">New Automation</h1>
          <p className="text-sm text-slate-500">Build an automation in a few simple steps.</p>
        </div>
      </div>

      {/* Progress */}
      <div className="flex items-center gap-0 mb-8">
        {steps.map((s, i) => (
          <React.Fragment key={s}>
            <div className="flex items-center gap-2">
              <div className={cn(
                "w-7 h-7 rounded-full flex items-center justify-center text-xs font-semibold transition-all",
                i < step ? "bg-green-500 text-white" : i === step ? "bg-blue-600 text-white" : "bg-slate-100 text-slate-400"
              )}>
                {i < step ? <Check className="w-3.5 h-3.5" /> : i + 1}
              </div>
              <span className={cn("text-xs font-medium hidden sm:block", i === step ? "text-blue-600" : "text-slate-400")}>{s}</span>
            </div>
            {i < steps.length - 1 && <div className={cn("flex-1 h-0.5 mx-2", i < step ? "bg-green-500" : "bg-slate-200")} />}
          </React.Fragment>
        ))}
      </div>

      <Card className="p-6">
        {step === 0 && (
          <div>
            <h2 className="text-base font-semibold text-slate-900 mb-1">Choose a Trigger</h2>
            <p className="text-sm text-slate-500 mb-5">What starts this automation?</p>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
              {triggers.map(t => (
                <button key={t.id} onClick={() => setTrigger(t.id)}
                  className={cn(
                    "p-4 rounded-xl border-2 text-left transition-all hover:shadow-md",
                    trigger === t.id ? "border-blue-500 bg-blue-50" : "border-slate-200 hover:border-blue-300"
                  )}>
                  <t.icon className={cn("w-6 h-6 mb-2", trigger === t.id ? "text-blue-600" : "text-slate-400")} />
                  <p className="text-sm font-semibold text-slate-900">{t.label}</p>
                  <p className="text-xs text-slate-400 mt-0.5">{t.desc}</p>
                </button>
              ))}
            </div>
          </div>
        )}

        {step === 1 && (
          <div className="space-y-5">
            <h2 className="text-base font-semibold text-slate-900">What should happen?</h2>
            <FormField label="Automation Name">
              <Input value={automName} onChange={setAutomName} placeholder="e.g. Application Verification" />
            </FormField>
            <FormField label="Description">
              <textarea rows={2} placeholder="Describe what this automation does…"
                className="w-full border border-slate-200 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" />
            </FormField>
            <div>
              <p className="text-sm font-medium text-slate-700 mb-3">Actions</p>
              <div className="space-y-2">
                {["Call API / Tool", "Send Notification", "Update Record", "AI Decision"].map(a => (
                  <label key={a} className="flex items-center gap-2.5 p-3 rounded-lg border border-slate-200 hover:bg-slate-50 cursor-pointer">
                    <input type="checkbox" className="rounded" />
                    <span className="text-sm text-slate-700">{a}</span>
                  </label>
                ))}
              </div>
            </div>
          </div>
        )}

        {step === 2 && (
          <div className="space-y-4">
            <h2 className="text-base font-semibold text-slate-900">Review your Automation</h2>
            <div className="bg-slate-50 rounded-xl p-4 space-y-3">
              <div className="flex items-center gap-3 p-3 bg-white rounded-lg border border-slate-200">
                <div className="w-8 h-8 rounded-lg bg-blue-100 flex items-center justify-center">
                  <Globe className="w-4 h-4 text-blue-600" />
                </div>
                <div>
                  <p className="text-xs text-slate-400">Trigger</p>
                  <p className="text-sm font-medium text-slate-900">{trigger ? triggers.find(t => t.id === trigger)?.label : "API Call"}</p>
                </div>
              </div>
              <ArrowDown className="w-4 h-4 text-slate-300 mx-auto" />
              <div className="flex items-center gap-3 p-3 bg-white rounded-lg border border-slate-200">
                <div className="w-8 h-8 rounded-lg bg-purple-100 flex items-center justify-center">
                  <Bot className="w-4 h-4 text-purple-600" />
                </div>
                <div>
                  <p className="text-xs text-slate-400">Action</p>
                  <p className="text-sm font-medium text-slate-900">Call API / Tool → Send Notification</p>
                </div>
              </div>
            </div>
            <p className="text-xs text-slate-400">Review looks good? Click Next to test this automation.</p>
          </div>
        )}

        {step === 3 && (
          <div className="space-y-4">
            <h2 className="text-base font-semibold text-slate-900">Test your Automation</h2>
            <p className="text-sm text-slate-500">Run a test execution to verify everything works correctly.</p>
            <div className="bg-slate-50 rounded-xl p-4">
              <p className="text-xs font-medium text-slate-700 mb-2">Test Input</p>
              <div className="bg-slate-900 rounded-lg p-3 font-mono text-xs text-green-400">
                {`{\n  "applicant_id": "TEST-001",\n  "documents": ["aadhaar", "income_proof"]\n}`}
              </div>
            </div>
            <Btn variant="primary" size="md" icon={<Play className="w-4 h-4" />} className="w-full justify-center">
              Run Test
            </Btn>
            <div className="bg-green-50 rounded-xl p-4 border border-green-200">
              <div className="flex items-center gap-2 mb-2">
                <CheckCircle className="w-4 h-4 text-green-600" />
                <span className="text-sm font-semibold text-green-800">Test Passed</span>
              </div>
              <p className="text-xs text-green-700">All 4 steps completed successfully in 1.2s</p>
            </div>
          </div>
        )}

        {step === 4 && (
          <div className="text-center py-6 space-y-4">
            <div className="w-16 h-16 rounded-full bg-green-100 flex items-center justify-center mx-auto">
              <Rocket className="w-8 h-8 text-green-600" />
            </div>
            <h2 className="text-lg font-semibold text-slate-900">Ready to Publish!</h2>
            <p className="text-sm text-slate-500">Your automation is configured and tested. Publish it to make it live.</p>
            <div className="flex justify-center gap-3">
              <Btn variant="outline" size="md">Save as Draft</Btn>
              <Btn variant="primary" size="md" icon={<Rocket className="w-4 h-4" />} onClick={() => onNavigate("automations")}>Publish Automation</Btn>
            </div>
          </div>
        )}
      </Card>

      <div className="flex items-center justify-between mt-5">
        <Btn variant="ghost" size="sm" onClick={() => step > 0 ? setStep(step - 1) : onNavigate("automations")}>
          <ChevronLeft className="w-4 h-4" /> {step === 0 ? "Cancel" : "Back"}
        </Btn>
        {step < steps.length - 1 && (
          <Btn variant="primary" size="sm" onClick={() => setStep(step + 1)} disabled={step === 0 && !trigger}>
            Continue <ChevronRight className="w-4 h-4" />
          </Btn>
        )}
      </div>
    </div>
  );
}

// ─── SCREEN: Advanced Workflow Builder ────────────────────────────────────────
function AdvancedWorkflowBuilder({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  const nodes = [
    { id: "trigger", label: "Trigger", sub: "Application Submitted", x: 80, y: 120, color: "blue", icon: Globe },
    { id: "ai", label: "AI / Decision", sub: "Extract & Validate", x: 280, y: 120, color: "purple", icon: Bot },
    { id: "condition", label: "Condition", sub: "Documents Valid?", x: 480, y: 120, color: "amber", icon: GitBranch },
    { id: "api", label: "Action", sub: "Update Application", x: 680, y: 60, color: "blue", icon: Code },
    { id: "reject", label: "Action", sub: "Notify Rejection", x: 680, y: 180, color: "red", icon: XCircle },
    { id: "approval", label: "Approval", sub: "Human / Prio Based", x: 880, y: 60, color: "pink", icon: UserCheck },
    { id: "notify", label: "Notification", sub: "Update Citizen", x: 1080, y: 60, color: "green", icon: Bell },
    { id: "end", label: "Response", sub: "Confirmation Sent", x: 1280, y: 120, color: "green", icon: CheckCircle },
  ];

  const colorMap: Record<string, string> = {
    blue: "bg-blue-50 border-blue-300 text-blue-700",
    purple: "bg-purple-50 border-purple-300 text-purple-700",
    amber: "bg-amber-50 border-amber-300 text-amber-700",
    red: "bg-red-50 border-red-300 text-red-700",
    green: "bg-green-50 border-green-300 text-green-700",
    pink: "bg-pink-50 border-pink-300 text-pink-700",
  };

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="px-6 py-4 border-b border-slate-200 bg-white flex items-center justify-between">
        <div className="flex items-center gap-3">
          <button onClick={() => onNavigate("automations")} className="p-1 rounded hover:bg-slate-100 text-slate-400">
            <ChevronLeft className="w-5 h-5" />
          </button>
          <div>
            <h1 className="text-base font-semibold text-slate-900">Application Verification</h1>
            <p className="text-xs text-slate-500">Advanced Workflow Builder</p>
          </div>
          <Badge variant="warning">Advanced</Badge>
        </div>
        <div className="flex items-center gap-2">
          <Btn variant="outline" size="sm" icon={<Play className="w-3.5 h-3.5" />}>Test</Btn>
          <Btn variant="secondary" size="sm" icon={<Save className="w-3.5 h-3.5" />}>Save</Btn>
          <Btn variant="primary" size="sm" icon={<Rocket className="w-3.5 h-3.5" />}>Publish</Btn>
        </div>
      </div>

      <div className="flex flex-1 overflow-hidden">
        {/* Canvas */}
        <div className="flex-1 bg-slate-50 overflow-auto relative">
          <div className="absolute inset-0" style={{ backgroundImage: "radial-gradient(circle, #CBD5E1 1px, transparent 1px)", backgroundSize: "24px 24px" }} />

          {/* Zoom controls */}
          <div className="absolute bottom-4 right-4 flex flex-col gap-1 z-10">
            {["+", "−", "⊡"].map((z, i) => (
              <button key={i} className="w-8 h-8 bg-white rounded-lg border border-slate-200 text-sm text-slate-600 hover:bg-slate-50 shadow-sm">{z}</button>
            ))}
            <div className="text-center text-xs text-slate-400 mt-1">100%</div>
          </div>

          {/* Nodes */}
          <div className="relative" style={{ minWidth: 1440, minHeight: 320, padding: "40px 40px" }}>
            {/* Connecting lines SVG */}
            <svg className="absolute inset-0 pointer-events-none" width="100%" height="100%" style={{ zIndex: 0 }}>
              {/* Main flow */}
              <path d="M 212 160 L 280 160" stroke="#CBD5E1" strokeWidth="2" fill="none" markerEnd="url(#arrow)" />
              <path d="M 412 160 L 480 160" stroke="#CBD5E1" strokeWidth="2" fill="none" />
              <path d="M 580 140 L 640 100 L 680 100" stroke="#16A34A" strokeWidth="2" fill="none" strokeDasharray="4,2" />
              <path d="M 580 175 L 640 215 L 680 215" stroke="#DC2626" strokeWidth="2" fill="none" strokeDasharray="4,2" />
              <path d="M 812 100 L 880 100" stroke="#CBD5E1" strokeWidth="2" fill="none" />
              <path d="M 1012 100 L 1080 100" stroke="#CBD5E1" strokeWidth="2" fill="none" />
              <path d="M 1212 100 L 1280 160" stroke="#CBD5E1" strokeWidth="2" fill="none" />
              <path d="M 812 215 L 1280 160" stroke="#CBD5E1" strokeWidth="1.5" strokeDasharray="3,3" fill="none" />
              <defs>
                <marker id="arrow" markerWidth="6" markerHeight="6" refX="3" refY="3" orient="auto">
                  <path d="M0,0 L6,3 L0,6 Z" fill="#CBD5E1" />
                </marker>
              </defs>
              {/* Yes/No labels */}
              <text x="648" y="128" fontSize="10" fill="#16A34A" fontWeight="600">Yes</text>
              <text x="648" y="245" fontSize="10" fill="#DC2626" fontWeight="600">No</text>
            </svg>

            {/* Node components */}
            {nodes.map(n => {
              const Icon = n.icon;
              return (
                <div key={n.id} className="absolute" style={{ left: n.x, top: n.y - 40 }}>
                  <div className={cn(
                    "w-28 rounded-xl border-2 p-3 shadow-sm cursor-pointer hover:shadow-md transition-all relative z-10",
                    colorMap[n.color]
                  )}>
                    <Icon className="w-4 h-4 mb-1.5" />
                    <p className="text-xs font-semibold leading-tight">{n.label}</p>
                    <p className="text-[10px] opacity-70 mt-0.5 leading-tight">{n.sub}</p>
                    <button className="absolute -top-1.5 -right-1.5 w-4 h-4 bg-slate-600 rounded-full flex items-center justify-center opacity-0 hover:opacity-100 group-hover:opacity-100">
                      <Plus className="w-2.5 h-2.5 text-white" />
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Right Panel */}
        <div className="w-64 bg-white border-l border-slate-200 p-4 overflow-y-auto">
          <h3 className="text-sm font-semibold text-slate-900 mb-3">Components</h3>
          <div className="space-y-1 mb-5">
            {[
              { label: "Trigger", icon: Globe, color: "text-blue-600 bg-blue-50" },
              { label: "AI / Decision", icon: Bot, color: "text-purple-600 bg-purple-50" },
              { label: "Condition", icon: GitBranch, color: "text-amber-600 bg-amber-50" },
              { label: "Action", icon: Code, color: "text-blue-600 bg-blue-50" },
              { label: "Approval", icon: UserCheck, color: "text-pink-600 bg-pink-50" },
              { label: "Notification", icon: Bell, color: "text-green-600 bg-green-50" },
              { label: "Transform", icon: Layers, color: "text-slate-600 bg-slate-100" },
              { label: "End", icon: CheckCircle, color: "text-green-600 bg-green-50" },
            ].map(c => (
              <div key={c.label}
                className="flex items-center gap-2.5 p-2 rounded-lg border border-slate-200 cursor-grab hover:bg-slate-50 hover:border-blue-200 transition-colors">
                <div className={cn("w-6 h-6 rounded-md flex items-center justify-center shrink-0", c.color)}>
                  <c.icon className="w-3.5 h-3.5" />
                </div>
                <span className="text-xs font-medium text-slate-700">{c.label}</span>
              </div>
            ))}
          </div>
          <div className="border-t border-slate-200 pt-4">
            <h3 className="text-sm font-semibold text-slate-900 mb-3">Selected Node</h3>
            <p className="text-xs text-slate-400 mb-2">AI Check — Application Submitted</p>
            <div className="space-y-2">
              <div>
                <label className="text-xs text-slate-500">Model</label>
                <Select value="local" options={[{ value: "local", label: "R2WAI LLM (Local)" }]} className="w-full mt-1" />
              </div>
              <div>
                <label className="text-xs text-slate-500">Next Step</label>
                <Select value="condition" options={[{ value: "condition", label: "Condition" }]} className="w-full mt-1" />
              </div>
            </div>
            <button className="mt-4 w-full text-xs text-red-600 hover:text-red-700 hover:bg-red-50 rounded-lg py-2 border border-red-200 transition-colors">
              Delete Node
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── SCREEN: Knowledge ─────────────────────────────────────────────────────────
function KnowledgeList({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  const [search, setSearch] = useState("");
  return (
    <div className="p-6">
      <SectionHeader
        title="Knowledge"
        subtitle="Manage knowledge bases and content sources."
        action={<Btn variant="primary" size="sm" icon={<Plus className="w-3.5 h-3.5" />} onClick={() => onNavigate("knowledgeEdit")}>Add Knowledge</Btn>}
      />
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
        <StatCard label="Knowledge Bases" value="5" icon={<BookOpen className="w-5 h-5" />} color="blue" />
        <StatCard label="Total Documents" value="610" delta="8%" deltaUp icon={<FileText className="w-5 h-5" />} color="purple" />
        <StatCard label="Active Sources" value="4" icon={<CheckCircle className="w-5 h-5" />} color="green" />
        <StatCard label="Syncing" value="1" icon={<RefreshCw className="w-5 h-5" />} color="amber" />
      </div>
      <Card>
        <div className="p-4 border-b border-slate-200 flex items-center gap-3">
          <Input className="w-60" placeholder="Search knowledge bases…" value={search} onChange={setSearch} icon={<Search className="w-4 h-4" />} />
          <Select value="All Types" options={[{ value: "All Types", label: "All Types" }, { value: "Document", label: "Document" }, { value: "Website", label: "Website" }, { value: "Database", label: "Database" }]} />
        </div>
        <Table
          columns={[
            { key: "name", label: "Name" },
            { key: "type", label: "Type" },
            { key: "docs", label: "Documents" },
            { key: "status", label: "Status" },
            { key: "updated", label: "Last Updated" },
            { key: "actions", label: "Actions" },
          ]}
          rows={knowledgeBases.map(k => ({
            name: (
              <div className="flex items-center gap-2.5">
                <div className="w-7 h-7 rounded-lg bg-blue-50 flex items-center justify-center">
                  <BookOpen className="w-3.5 h-3.5 text-blue-600" />
                </div>
                <span className="font-medium text-slate-900">{k.name}</span>
              </div>
            ),
            type: <Badge variant="default">{k.type}</Badge>,
            docs: <span className="text-sm text-slate-600">{k.docs.toLocaleString()}</span>,
            status: <StatusBadge status={k.status} />,
            updated: <span className="text-xs text-slate-500">{k.updated}</span>,
            actions: (
              <div className="flex items-center gap-1">
                <button className="p-1.5 rounded hover:bg-slate-100 text-slate-400 hover:text-blue-600"><Eye className="w-3.5 h-3.5" /></button>
                <button className="p-1.5 rounded hover:bg-slate-100 text-slate-400 hover:text-blue-600" onClick={() => onNavigate("knowledgeEdit")}><Edit className="w-3.5 h-3.5" /></button>
                <button className="p-1.5 rounded hover:bg-slate-100 text-slate-400 hover:text-blue-600"><RefreshCw className="w-3.5 h-3.5" /></button>
                <button className="p-1.5 rounded hover:bg-red-50 text-slate-400 hover:text-red-600"><Trash2 className="w-3.5 h-3.5" /></button>
              </div>
            ),
          }))}
        />
      </Card>
    </div>
  );
}

// ─── SCREEN: Knowledge Edit ────────────────────────────────────────────────────
function KnowledgeEditScreen({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  const [sourceType, setSourceType] = useState("document");
  return (
    <div className="p-6 max-w-2xl mx-auto">
      <div className="flex items-center gap-3 mb-6">
        <button onClick={() => onNavigate("knowledge")} className="p-1 rounded hover:bg-slate-100 text-slate-400">
          <ChevronLeft className="w-5 h-5" />
        </button>
        <h1 className="text-xl font-semibold text-slate-900">Add Knowledge Source</h1>
      </div>
      <Card className="p-6 space-y-5">
        <FormField label="Name">
          <Input placeholder="e.g. Citizen Services FAQ" />
        </FormField>
        <FormField label="Source Type">
          <div className="grid grid-cols-3 gap-3">
            {[
              { id: "document", label: "Documents", icon: FileText },
              { id: "website", label: "Website", icon: Globe },
              { id: "database", label: "Database", icon: Database },
            ].map(t => (
              <button key={t.id} onClick={() => setSourceType(t.id)}
                className={cn("p-3 rounded-xl border-2 flex flex-col items-center gap-2 transition-all",
                  sourceType === t.id ? "border-blue-500 bg-blue-50" : "border-slate-200 hover:border-slate-300")}>
                <t.icon className={cn("w-5 h-5", sourceType === t.id ? "text-blue-600" : "text-slate-400")} />
                <span className="text-xs font-medium text-slate-700">{t.label}</span>
              </button>
            ))}
          </div>
        </FormField>
        {sourceType === "document" && (
          <FormField label="Upload Files">
            <div className="border-2 border-dashed border-slate-200 rounded-xl p-8 text-center hover:border-blue-300 hover:bg-blue-50 transition-all cursor-pointer">
              <Upload className="w-8 h-8 text-slate-300 mx-auto mb-2" />
              <p className="text-sm text-slate-600 font-medium">Click to upload or drag & drop</p>
              <p className="text-xs text-slate-400 mt-1">PDF, DOCX, TXT, MD up to 50MB</p>
            </div>
          </FormField>
        )}
        {sourceType === "website" && (
          <FormField label="Website URL" hint="We'll crawl and index this website automatically.">
            <Input placeholder="https://example.gov.in" icon={<Globe className="w-4 h-4" />} />
          </FormField>
        )}
        {sourceType === "database" && (
          <FormField label="Connection String" hint="Database will be queried in real-time.">
            <Input placeholder="postgresql://user:pass@host/db" icon={<Database className="w-4 h-4" />} />
          </FormField>
        )}
        <FormField label="Auto-sync">
          <div className="flex items-center gap-2">
            <input type="checkbox" defaultChecked className="rounded" />
            <span className="text-sm text-slate-600">Automatically sync every 24 hours</span>
          </div>
        </FormField>
        <div className="flex justify-end gap-3 pt-2">
          <Btn variant="ghost" size="sm" onClick={() => onNavigate("knowledge")}>Cancel</Btn>
          <Btn variant="primary" size="sm" icon={<Save className="w-3.5 h-3.5" />}>Add Knowledge Source</Btn>
        </div>
      </Card>
    </div>
  );
}

// ─── SCREEN: Integrations ─────────────────────────────────────────────────────
function IntegrationsList({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  return (
    <div className="p-6">
      <SectionHeader
        title="Integrations"
        subtitle="Connect external services and platforms."
        action={<Btn variant="primary" size="sm" icon={<Plus className="w-3.5 h-3.5" />} onClick={() => onNavigate("integrationEdit")}>Add Integration</Btn>}
      />
      <div className="mb-6">
        <h2 className="text-sm font-semibold text-slate-700 mb-3">Connected ({integrations.filter(i => i.status === "Connected").length})</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {integrations.map(integ => (
            <Card key={integ.id} className="p-4 hover:shadow-md transition-shadow">
              <div className="flex items-start justify-between">
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 rounded-xl bg-slate-100 flex items-center justify-center">
                    {integ.category === "Identity" && <Shield className="w-5 h-5 text-blue-600" />}
                    {integ.category === "Communication" && <MessageSquare className="w-5 h-5 text-green-600" />}
                    {integ.category === "Finance" && <BarChart2 className="w-5 h-5 text-amber-600" />}
                    {integ.category === "Storage" && <Database className="w-5 h-5 text-purple-600" />}
                    {integ.category === "Internal" && <Server className="w-5 h-5 text-slate-500" />}
                  </div>
                  <div>
                    <p className="text-sm font-semibold text-slate-900">{integ.name}</p>
                    <p className="text-xs text-slate-400">{integ.category}</p>
                  </div>
                </div>
                <StatusBadge status={integ.status} />
              </div>
              <div className="mt-3 flex items-center justify-between">
                <span className="text-xs text-slate-400">Last sync: {integ.lastSync}</span>
                <div className="flex gap-1">
                  <button className="p-1.5 rounded hover:bg-slate-100 text-slate-400 hover:text-blue-600" onClick={() => onNavigate("integrationEdit")}><Edit className="w-3.5 h-3.5" /></button>
                  <button className="p-1.5 rounded hover:bg-slate-100 text-slate-400 hover:text-blue-600"><RefreshCw className="w-3.5 h-3.5" /></button>
                </div>
              </div>
            </Card>
          ))}
        </div>
      </div>
      <div>
        <h2 className="text-sm font-semibold text-slate-700 mb-3">Available Integrations</h2>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          {["WhatsApp Business", "Telegram", "Slack", "Google Workspace", "Microsoft 365", "Salesforce", "SAP", "Custom API"].map(n => (
            <button key={n} onClick={() => onNavigate("integrationEdit")}
              className="p-4 rounded-xl border-2 border-dashed border-slate-200 hover:border-blue-300 hover:bg-blue-50 transition-all text-center group">
              <Plug className="w-5 h-5 text-slate-300 mx-auto mb-2 group-hover:text-blue-500" />
              <p className="text-xs font-medium text-slate-500 group-hover:text-blue-700">{n}</p>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

// ─── SCREEN: Integration Edit ─────────────────────────────────────────────────
function IntegrationEditScreen({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  return (
    <div className="p-6 max-w-xl mx-auto">
      <div className="flex items-center gap-3 mb-6">
        <button onClick={() => onNavigate("integrations")} className="p-1 rounded hover:bg-slate-100 text-slate-400">
          <ChevronLeft className="w-5 h-5" />
        </button>
        <h1 className="text-xl font-semibold text-slate-900">Configure Integration</h1>
      </div>
      <Card className="p-6 space-y-5">
        <div className="flex items-center gap-3 p-4 bg-slate-50 rounded-xl">
          <div className="w-10 h-10 rounded-xl bg-green-100 flex items-center justify-center">
            <MessageSquare className="w-5 h-5 text-green-600" />
          </div>
          <div>
            <p className="text-sm font-semibold text-slate-900">SMS Gateway (Twilio)</p>
            <p className="text-xs text-slate-400">Communication · Connected</p>
          </div>
        </div>
        <FormField label="Account SID">
          <Input placeholder="ACxxxxxxxxxxxxxxxx" icon={<Key className="w-4 h-4" />} />
        </FormField>
        <FormField label="Auth Token">
          <Input placeholder="••••••••••••••••" icon={<Lock className="w-4 h-4" />} />
        </FormField>
        <FormField label="From Number">
          <Input placeholder="+91 XXXXXXXXXX" />
        </FormField>
        <FormField label="Webhook URL" hint="Receive delivery status updates at this URL.">
          <Input placeholder="https://r2wai.gov.in/webhooks/sms" icon={<Globe className="w-4 h-4" />} />
        </FormField>
        <FormField label="Enabled">
          <div className="flex items-center gap-2">
            <input type="checkbox" defaultChecked className="rounded" />
            <span className="text-sm text-slate-600">Active and receiving events</span>
          </div>
        </FormField>
        <div className="flex justify-between gap-3 pt-2">
          <Btn variant="outline" size="sm" icon={<Play className="w-3.5 h-3.5" />}>Test Connection</Btn>
          <div className="flex gap-2">
            <Btn variant="ghost" size="sm" onClick={() => onNavigate("integrations")}>Cancel</Btn>
            <Btn variant="primary" size="sm" icon={<Save className="w-3.5 h-3.5" />}>Save Integration</Btn>
          </div>
        </div>
      </Card>
    </div>
  );
}

// ─── SCREEN: Tools & APIs ──────────────────────────────────────────────────────
function ToolsAPIs({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  const [tab, setTab] = useState("tools");
  return (
    <div className="p-6">
      <SectionHeader
        title="Tools & APIs"
        subtitle="Manage tools and API connections available to your AI assistants."
        action={<Btn variant="primary" size="sm" icon={<Plus className="w-3.5 h-3.5" />}>Add Tool</Btn>}
      />
      <Tabs tabs={[{ id: "tools", label: "Tools" }, { id: "apis", label: "APIs" }, { id: "webhooks", label: "Webhooks" }]} active={tab} onChange={setTab} />
      <div className="mt-4">
        <Card>
          <Table
            columns={[
              { key: "name", label: "Tool Name" },
              { key: "type", label: "Type" },
              { key: "endpoint", label: "Endpoint" },
              { key: "auth", label: "Auth" },
              { key: "calls", label: "API Calls" },
              { key: "status", label: "Status" },
              { key: "actions", label: "Actions" },
            ]}
            rows={tools.map(t => ({
              name: <span className="font-medium text-slate-900">{t.name}</span>,
              type: <Badge variant="blue">{t.type}</Badge>,
              endpoint: <code className="text-xs text-slate-600 font-mono bg-slate-50 px-2 py-0.5 rounded">{t.endpoint}</code>,
              auth: <Badge variant="purple">{t.auth}</Badge>,
              calls: <span className="text-sm text-slate-600">{t.calls.toLocaleString()}</span>,
              status: <StatusBadge status={t.status} />,
              actions: (
                <div className="flex items-center gap-1">
                  <button className="p-1.5 rounded hover:bg-slate-100 text-slate-400 hover:text-blue-600"><Play className="w-3.5 h-3.5" /></button>
                  <button className="p-1.5 rounded hover:bg-slate-100 text-slate-400 hover:text-blue-600"><Edit className="w-3.5 h-3.5" /></button>
                  <button className="p-1.5 rounded hover:bg-slate-100 text-slate-400 hover:text-blue-600"><Copy className="w-3.5 h-3.5" /></button>
                  <button className="p-1.5 rounded hover:bg-red-50 text-slate-400 hover:text-red-600"><Trash2 className="w-3.5 h-3.5" /></button>
                </div>
              ),
            }))}
          />
        </Card>
      </div>
    </div>
  );
}

// ─── SCREEN: Publish ──────────────────────────────────────────────────────────
function PublishScreen({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  const envs = [
    { name: "Development", status: "Active", url: "https://dev.r2wai.gov.in", lastDeploy: "12 May 2025 09:00", assistants: 12, automations: 28 },
    { name: "Staging", status: "Active", url: "https://staging.r2wai.gov.in", lastDeploy: "10 May 2025 14:30", assistants: 8, automations: 20 },
    { name: "Production", status: "Published", url: "https://r2wai.gov.in", lastDeploy: "8 May 2025 10:00", assistants: 6, automations: 15 },
  ];
  return (
    <div className="p-6">
      <SectionHeader
        title="Publish"
        subtitle="Deploy your AI assistants and automations to environments."
        action={<Btn variant="primary" size="sm" icon={<Rocket className="w-3.5 h-3.5" />}>Deploy Now</Btn>}
      />
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        {envs.map(e => (
          <Card key={e.name} className="p-5">
            <div className="flex items-center justify-between mb-3">
              <div className="flex items-center gap-2">
                <div className={cn("w-2 h-2 rounded-full", e.status === "Published" ? "bg-green-500" : "bg-blue-500")} />
                <h3 className="text-sm font-semibold text-slate-900">{e.name}</h3>
              </div>
              <StatusBadge status={e.status} />
            </div>
            <div className="space-y-2 text-xs text-slate-500">
              <div className="flex items-center gap-1.5">
                <Globe className="w-3.5 h-3.5" />
                <a href="#" className="text-blue-600 hover:underline truncate">{e.url}</a>
              </div>
              <div className="flex items-center gap-1.5">
                <Clock className="w-3.5 h-3.5" />
                {e.lastDeploy}
              </div>
              <div className="flex items-center gap-4 mt-2 pt-2 border-t border-slate-100">
                <span><strong className="text-slate-900">{e.assistants}</strong> Assistants</span>
                <span><strong className="text-slate-900">{e.automations}</strong> Automations</span>
              </div>
            </div>
            <div className="flex gap-2 mt-4">
              <Btn variant="secondary" size="xs" className="flex-1 justify-center">View</Btn>
              <Btn variant="primary" size="xs" className="flex-1 justify-center" icon={<Rocket className="w-3 h-3" />}>Deploy</Btn>
            </div>
          </Card>
        ))}
      </div>
      <Card className="p-5">
        <h3 className="text-sm font-semibold text-slate-900 mb-4">Ready to Publish</h3>
        <Table
          columns={[
            { key: "name", label: "Name" },
            { key: "type", label: "Type" },
            { key: "status", label: "Status" },
            { key: "env", label: "Target Env" },
            { key: "actions", label: "Actions" },
          ]}
          rows={[
            { name: "Citizen Support", type: <Badge variant="blue">Assistant</Badge>, status: <StatusBadge status="Active" />, env: "Production", actions: <Btn variant="primary" size="xs" icon={<Rocket className="w-3 h-3" />}>Publish</Btn> },
            { name: "Application Verification", type: <Badge variant="purple">Automation</Badge>, status: <StatusBadge status="Active" />, env: "Staging", actions: <Btn variant="secondary" size="xs">Review</Btn> },
            { name: "FAQ Assistant", type: <Badge variant="blue">Assistant</Badge>, status: <StatusBadge status="Draft" />, env: "Development", actions: <Btn variant="outline" size="xs">Edit</Btn> },
          ]}
        />
      </Card>
    </div>
  );
}

// ─── SCREEN: Monitor ──────────────────────────────────────────────────────────
function MonitorDashboard({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  const [search, setSearch] = useState("");
  return (
    <div className="p-6">
      <SectionHeader
        title="Monitor"
        subtitle="Track and monitor automation executions."
        action={
          <div className="flex gap-2">
            <Btn variant="ghost" size="sm" icon={<RefreshCw className="w-3.5 h-3.5" />}>Refresh</Btn>
            <Btn variant="outline" size="sm" icon={<Download className="w-3.5 h-3.5" />}>Export</Btn>
          </div>
        }
      />
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
        <StatCard label="Total Executions" value="1,248" delta="22%" deltaUp icon={<Activity className="w-5 h-5" />} color="blue" />
        <StatCard label="Successful" value="1,230" delta="1.4%" deltaUp icon={<CheckCircle className="w-5 h-5" />} color="green" />
        <StatCard label="Failed" value="10" delta="6%" deltaUp={false} icon={<XCircle className="w-5 h-5" />} color="red" />
        <StatCard label="In Progress" value="8" icon={<Clock className="w-5 h-5" />} color="amber" />
      </div>
      <Card>
        <div className="p-4 border-b border-slate-200 flex items-center gap-3 flex-wrap">
          <Input className="w-60" placeholder="Search executions…" value={search} onChange={setSearch} icon={<Search className="w-4 h-4" />} />
          <Select value="All Status" options={[{ value: "All Status", label: "All Status" }, { value: "Success", label: "Success" }, { value: "Failed", label: "Failed" }, { value: "In Progress", label: "In Progress" }]} />
          <Select value="All Automations" options={[{ value: "All Automations", label: "All Automations" }, ...automations.map(a => ({ value: a.name, label: a.name }))]} />
          <span className="text-xs text-slate-400 ml-auto">12 May 2025 – 18 May 2025</span>
        </div>
        <Table
          columns={[
            { key: "id", label: "Execution ID" },
            { key: "name", label: "Automation" },
            { key: "trigger", label: "Trigger" },
            { key: "status", label: "Status" },
            { key: "duration", label: "Duration" },
            { key: "started", label: "Started At" },
            { key: "actions", label: "Actions" },
          ]}
          rows={executions.map(e => ({
            id: <code className="text-xs text-blue-600 font-mono">{e.id}</code>,
            name: <span className="font-medium text-slate-900">{e.name}</span>,
            trigger: <Badge variant="default">{e.trigger}</Badge>,
            status: <StatusBadge status={e.status} />,
            duration: <span className="text-xs text-slate-600">{e.duration}</span>,
            started: <span className="text-xs text-slate-500">{e.started}</span>,
            actions: (
              <Btn variant="ghost" size="xs" onClick={() => onNavigate("executionDetails")}>
                <Eye className="w-3.5 h-3.5" /> Details
              </Btn>
            ),
          }))}
          onRowClick={() => onNavigate("executionDetails")}
        />
        <div className="p-4 flex items-center justify-between border-t border-slate-200">
          <span className="text-xs text-slate-500">Showing 1–5 of 1,248 executions</span>
          <div className="flex items-center gap-1">
            {[1, 2, 3, "...", 250].map((p, i) => (
              <button key={i} className={cn("min-w-7 h-7 px-2 text-xs rounded-md", p === 1 ? "bg-blue-600 text-white" : "text-slate-500 hover:bg-slate-100")}>{p}</button>
            ))}
          </div>
        </div>
      </Card>
    </div>
  );
}

// ─── SCREEN: Execution Details ────────────────────────────────────────────────
function ExecutionDetailsScreen({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  const steps = [
    { label: "Trigger Received", time: "09:30:00", duration: "0ms", status: "success", detail: "POST /api/v1/application/verify — Applicant ID: APP-20250001" },
    { label: "Input Validated", time: "09:30:00", duration: "12ms", status: "success", detail: "All required fields present" },
    { label: "AI Check — Document Analysis", time: "09:30:00", duration: "843ms", status: "success", detail: "3/3 documents validated. Confidence: 98.4%" },
    { label: "Condition: Documents Valid?", time: "09:30:01", duration: "2ms", status: "success", detail: "Result: YES → Proceeding to approval" },
    { label: "Action: Update Application Status", time: "09:30:01", duration: "234ms", status: "success", detail: "Status updated to: Under Review" },
    { label: "Notify Officer", time: "09:30:01", duration: "156ms", status: "success", detail: "Email sent to officer@district.gov.in" },
    { label: "Documents Complete?", time: "09:30:01", duration: "1ms", status: "success", detail: "Result: YES → Triggering Notify Citizen" },
    { label: "Notify Citizen", time: "09:30:02", duration: "189ms", status: "success", detail: "SMS sent to +91-XXXXXXXXXX" },
  ];
  return (
    <div className="p-6">
      <div className="flex items-center gap-3 mb-5">
        <button onClick={() => onNavigate("monitor")} className="p-1 rounded hover:bg-slate-100 text-slate-400">
          <ChevronLeft className="w-5 h-5" />
        </button>
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-semibold text-slate-900">EXE-1248</h1>
            <StatusBadge status="Success" />
          </div>
          <p className="text-sm text-slate-500 mt-0.5">Application Verification · 12 May 2025 09:30</p>
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-3 gap-5">
        <div className="xl:col-span-2">
          <Card className="p-5">
            <h3 className="text-sm font-semibold text-slate-900 mb-4">Execution Timeline</h3>
            <div className="space-y-0">
              {steps.map((s, i) => (
                <div key={i} className="flex gap-4">
                  <div className="flex flex-col items-center">
                    <div className={cn("w-6 h-6 rounded-full flex items-center justify-center shrink-0 mt-0.5",
                      s.status === "success" ? "bg-green-100" : "bg-red-100")}>
                      <CheckCircle className={cn("w-3.5 h-3.5", s.status === "success" ? "text-green-600" : "text-red-600")} />
                    </div>
                    {i < steps.length - 1 && <div className="w-px flex-1 bg-slate-200 my-1" style={{ minHeight: 24 }} />}
                  </div>
                  <div className="pb-4">
                    <div className="flex items-center gap-2 mb-0.5">
                      <span className="text-sm font-medium text-slate-900">{s.label}</span>
                      <span className="text-xs text-slate-400">{s.time}</span>
                      <span className="text-xs text-slate-400">({s.duration})</span>
                    </div>
                    <p className="text-xs text-slate-500">{s.detail}</p>
                  </div>
                </div>
              ))}
            </div>
          </Card>
        </div>

        <div className="space-y-4">
          <Card className="p-5">
            <h3 className="text-sm font-semibold text-slate-900 mb-3">Summary</h3>
            <div className="space-y-2.5">
              {[
                { label: "Status", value: <Badge variant="success">Success</Badge> },
                { label: "Total Duration", value: "3.24 sec" },
                { label: "Steps", value: "8" },
                { label: "Model", value: "R2WAI LLM" },
                { label: "Trigger", value: "API" },
                { label: "Tokens Used", value: "258" },
              ].map(d => (
                <div key={d.label} className="flex items-center justify-between">
                  <span className="text-xs text-slate-500">{d.label}</span>
                  <span className="text-xs font-medium text-slate-900">{d.value}</span>
                </div>
              ))}
            </div>
          </Card>
          <Card className="p-5">
            <h3 className="text-sm font-semibold text-slate-900 mb-3">Input Payload</h3>
            <div className="bg-slate-900 rounded-lg p-3 font-mono text-xs text-green-400">
              {`{\n  "applicant_id": "APP-001",\n  "docs": ["aadhaar", "income"]\n}`}
            </div>
          </Card>
          <Card className="p-5">
            <h3 className="text-sm font-semibold text-slate-900 mb-3">Output</h3>
            <div className="bg-slate-900 rounded-lg p-3 font-mono text-xs text-blue-300">
              {`{\n  "status": "Under Review",\n  "officer_notified": true,\n  "citizen_sms": "sent"\n}`}
            </div>
          </Card>
        </div>
      </div>
    </div>
  );
}

// ─── SCREEN: Users & Roles ────────────────────────────────────────────────────
function UsersRolesScreen({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  const [tab, setTab] = useState("users");
  const [addUser, setAddUser] = useState(false);

  return (
    <div className="p-6">
      <SectionHeader
        title="Users & Roles"
        subtitle="Manage platform users and access control."
        action={
          tab === "users"
            ? <Btn variant="primary" size="sm" icon={<Plus className="w-3.5 h-3.5" />} onClick={() => setAddUser(true)}>Add User</Btn>
            : tab === "permissions"
              ? <Btn variant="secondary" size="sm" onClick={() => onNavigate("permissionsMatrix")}>Full Matrix</Btn>
              : <Btn variant="primary" size="sm" icon={<Plus className="w-3.5 h-3.5" />}>Add Role</Btn>
        }
      />

      <Tabs tabs={[{ id: "users", label: "Users" }, { id: "roles", label: "Roles" }, { id: "permissions", label: "Permissions" }]} active={tab} onChange={setTab} />

      <div className="mt-4">
        {tab === "users" && (
          <Card>
            <div className="p-4 border-b border-slate-200 flex items-center gap-3">
              <Input className="w-60" placeholder="Search users…" icon={<Search className="w-4 h-4" />} />
              <Select value="All Roles" options={[{ value: "All Roles", label: "All Roles" }, { value: "Admin", label: "Admin" }, { value: "User", label: "User" }]} />
            </div>
            <Table
              columns={[
                { key: "user", label: "User" },
                { key: "role", label: "Role" },
                { key: "status", label: "Status" },
                { key: "last", label: "Last Active" },
                { key: "actions", label: "Actions" },
              ]}
              rows={users.map(u => ({
                user: (
                  <div className="flex items-center gap-2.5">
                    <div className="w-8 h-8 rounded-full bg-blue-100 flex items-center justify-center text-xs font-semibold text-blue-700">
                      {u.name.split(" ").map(n => n[0]).join("")}
                    </div>
                    <div>
                      <p className="text-sm font-medium text-slate-900">{u.name}</p>
                      <p className="text-xs text-slate-400">{u.email}</p>
                    </div>
                  </div>
                ),
                role: <Badge variant={u.role === "Admin" ? "blue" : "default"}>{u.role}</Badge>,
                status: <StatusBadge status={u.status} />,
                last: <span className="text-xs text-slate-500">{u.last}</span>,
                actions: (
                  <div className="flex items-center gap-1">
                    <button className="p-1.5 rounded hover:bg-slate-100 text-slate-400 hover:text-blue-600"><Edit className="w-3.5 h-3.5" /></button>
                    <button className="p-1.5 rounded hover:bg-red-50 text-slate-400 hover:text-red-600"><Trash2 className="w-3.5 h-3.5" /></button>
                  </div>
                ),
              }))}
            />
          </Card>
        )}

        {tab === "roles" && (
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {[
              { name: "Super Admin", users: 2, desc: "Full platform governance", color: "red", perms: ["Platform Management", "Admin Management", "Users & Roles", "AI Models & Security", "Global Policies", "System Health", "Audit Logs"] },
              { name: "Admin", users: 8, desc: "Configuration and operations", color: "blue", perms: ["AI Assistants", "Automations", "Knowledge", "Integrations", "Tools & APIs", "Test & Playground", "Publish", "Monitor", "Users"] },
              { name: "User", users: 346, desc: "Daily usage", color: "green", perms: ["AI Assistant Chat", "View Automations", "Knowledge Access", "My Activity", "Notifications", "Profile"] },
            ].map(r => (
              <Card key={r.name} className="p-5">
                <div className="flex items-center justify-between mb-3">
                  <div>
                    <h3 className="text-sm font-semibold text-slate-900">{r.name}</h3>
                    <p className="text-xs text-slate-400 mt-0.5">{r.desc}</p>
                  </div>
                  <span className="text-xs text-slate-500 bg-slate-100 px-2 py-1 rounded-lg">{r.users} users</span>
                </div>
                <ul className="space-y-1.5 mb-4">
                  {r.perms.map(p => (
                    <li key={p} className="flex items-center gap-1.5 text-xs text-slate-600">
                      <Check className="w-3 h-3 text-green-500 shrink-0" />
                      {p}
                    </li>
                  ))}
                </ul>
                <div className="flex gap-2">
                  <Btn variant="ghost" size="xs">Edit</Btn>
                  <Btn variant="secondary" size="xs">Clone</Btn>
                </div>
              </Card>
            ))}
          </div>
        )}

        {tab === "permissions" && (
          <Card className="p-5">
            <p className="text-sm text-slate-500 mb-4">Quick overview of key permissions by role. <button onClick={() => onNavigate("permissionsMatrix")} className="text-blue-600 hover:underline">View full matrix →</button></p>
            <div className="overflow-x-auto">
              <table className="w-full text-xs">
                <thead>
                  <tr className="border-b border-slate-200">
                    <th className="text-left py-2 px-3 text-slate-500">Module</th>
                    {["Super Admin", "Admin", "User"].map(r => (
                      <th key={r} className="text-center py-2 px-3 text-slate-500">{r}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {["AI Assistants", "Automations", "Knowledge", "Integrations", "Tools & APIs", "Monitor", "Users & Roles", "Settings"].map(m => (
                    <tr key={m} className="border-b border-slate-100">
                      <td className="py-2 px-3 font-medium text-slate-700">{m}</td>
                      <td className="py-2 px-3 text-center"><CheckCircle className="w-3.5 h-3.5 text-green-500 mx-auto" /></td>
                      <td className="py-2 px-3 text-center">
                        {["Users & Roles", "Settings"].includes(m)
                          ? <Minus className="w-3.5 h-3.5 text-slate-300 mx-auto" />
                          : <CheckCircle className="w-3.5 h-3.5 text-green-500 mx-auto" />}
                      </td>
                      <td className="py-2 px-3 text-center">
                        {["AI Assistants", "Automations", "Knowledge"].includes(m)
                          ? <Eye className="w-3.5 h-3.5 text-blue-400 mx-auto" />
                          : <Minus className="w-3.5 h-3.5 text-slate-300 mx-auto" />}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        )}
      </div>

      <Modal open={addUser} onClose={() => setAddUser(false)} title="Add User">
        <div className="space-y-4">
          <FormField label="Full Name"><Input placeholder="e.g. Priya Sharma" /></FormField>
          <FormField label="Email"><Input placeholder="user@gov.in" /></FormField>
          <FormField label="Role">
            <Select value="User" options={[{ value: "User", label: "User" }, { value: "Admin", label: "Admin" }]} className="w-full" />
          </FormField>
          <div className="flex items-center gap-2">
            <input type="checkbox" defaultChecked className="rounded" />
            <span className="text-sm text-slate-600">Send welcome email with login credentials</span>
          </div>
        </div>
        <div className="flex justify-end gap-2 mt-5">
          <Btn variant="outline" size="sm" onClick={() => setAddUser(false)}>Cancel</Btn>
          <Btn variant="primary" size="sm" onClick={() => setAddUser(false)}>Add User</Btn>
        </div>
      </Modal>
    </div>
  );
}

// ─── SCREEN: Permissions Matrix ───────────────────────────────────────────────
function PermissionsMatrixScreen({ onNavigate }: { onNavigate: (s: Screen) => void }) {
  const modules = ["AI Assistants", "Automations", "Knowledge", "Integrations", "Tools & APIs", "Test & Playground", "Publish", "Monitor", "Users & Roles", "AI Models", "Security", "System Health", "Settings"];
  const actions = ["View", "Create", "Edit", "Delete", "Publish", "Execute"];
  const allowed: Record<string, Record<string, Record<string, boolean>>> = {};
  modules.forEach(m => {
    allowed[m] = { "Super Admin": {}, "Admin": {}, "User": {} };
    actions.forEach(a => {
      allowed[m]["Super Admin"][a] = true;
      allowed[m]["Admin"][a] = !["AI Models", "Security", "System Health"].includes(m) && a !== "Delete";
      allowed[m]["User"][a] = ["AI Assistants", "Automations", "Knowledge"].includes(m) && a === "View";
    });
  });

  return (
    <div className="p-6">
      <div className="flex items-center gap-3 mb-5">
        <button onClick={() => onNavigate("usersRoles")} className="p-1 rounded hover:bg-slate-100 text-slate-400">
          <ChevronLeft className="w-5 h-5" />
        </button>
        <h1 className="text-xl font-semibold text-slate-900">Permissions Matrix</h1>
      </div>
      <Card className="overflow-hidden">
        <div className="overflow-x-auto">
          <table className="text-xs w-full">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-200">
                <th className="text-left py-3 px-4 font-semibold text-slate-600 min-w-48">Module / Permission</th>
                {["Super Admin", "Admin", "User"].map(role => (
                  <th key={role} colSpan={6} className="text-center py-3 px-2 font-semibold text-slate-600 border-l border-slate-200">
                    {role}
                  </th>
                ))}
              </tr>
              <tr className="bg-slate-50 border-b border-slate-200">
                <th className="py-2 px-4 text-left text-slate-400 font-normal">Action →</th>
                {["Super Admin", "Admin", "User"].map(role =>
                  actions.map(a => <th key={`${role}-${a}`} className="py-2 px-2 text-center text-slate-400 font-normal">{a}</th>)
                )}
              </tr>
            </thead>
            <tbody>
              {modules.map((m, mi) => (
                <tr key={m} className={cn("border-b border-slate-100", mi % 2 === 0 ? "bg-white" : "bg-slate-50/50")}>
                  <td className="py-2.5 px-4 font-medium text-slate-700">{m}</td>
                  {["Super Admin", "Admin", "User"].map(role =>
                    actions.map(a => (
                      <td key={`${role}-${a}`} className="py-2.5 px-2 text-center">
                        {allowed[m]?.[role]?.[a]
                          ? <CheckCircle className="w-3.5 h-3.5 text-green-500 mx-auto" />
                          : <Minus className="w-3.5 h-3.5 text-slate-200 mx-auto" />}
                      </td>
                    ))
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="p-4 border-t border-slate-200 flex items-center gap-4 text-xs text-slate-500">
          <div className="flex items-center gap-1.5"><CheckCircle className="w-3.5 h-3.5 text-green-500" /> Allowed</div>
          <div className="flex items-center gap-1.5"><Minus className="w-3.5 h-3.5 text-slate-300" /> Not Allowed</div>
        </div>
      </Card>
    </div>
  );
}

// ─── SCREEN: Settings ──────────────────────────────────────────────────────────
function SettingsScreen() {
  const [tab, setTab] = useState("general");
  const settingsTabs = [
    { id: "general", label: "General" },
    { id: "security", label: "Security" },
    { id: "email", label: "Email & SMS" },
    { id: "storage", label: "Storage" },
    { id: "ai", label: "AI Models" },
    { id: "backup", label: "Backup" },
    { id: "notifications", label: "Notifications" },
    { id: "audit", label: "Audit" },
  ];
  return (
    <div className="p-6">
      <SectionHeader title="Settings" subtitle="Configure platform settings." />
      <div className="flex gap-6">
        {/* Sidebar nav */}
        <div className="w-44 shrink-0">
          <nav className="space-y-0.5">
            {settingsTabs.map(t => (
              <button key={t.id} onClick={() => setTab(t.id)}
                className={cn(
                  "w-full text-left px-3 py-2 rounded-lg text-sm font-medium transition-colors",
                  tab === t.id ? "bg-blue-50 text-blue-700" : "text-slate-600 hover:bg-slate-100"
                )}>
                {t.label}
              </button>
            ))}
          </nav>
        </div>

        <div className="flex-1 space-y-4">
          {tab === "general" && (
            <Card className="p-6 space-y-5">
              <h3 className="text-sm font-semibold text-slate-900">Platform</h3>
              <FormField label="Platform Name"><Input value="R2WAI" onChange={() => { }} /></FormField>
              <FormField label="Language">
                <Select value="en" options={[{ value: "en", label: "English" }, { value: "hi", label: "Hindi" }]} className="w-full" />
              </FormField>
              <FormField label="Time Zone">
                <Select value="ist" options={[{ value: "ist", label: "IST (GMT+5:30) Asia/Kolkata" }]} className="w-full" />
              </FormField>
              <FormField label="Date Format">
                <Select value="dmy" options={[{ value: "dmy", label: "DD-MM-YYYY" }, { value: "mdy", label: "MM/DD/YYYY" }]} className="w-full" />
              </FormField>
              <div className="flex justify-end">
                <Btn variant="primary" size="sm">Save Changes</Btn>
              </div>
            </Card>
          )}
          {tab === "security" && (
            <Card className="p-6 space-y-5">
              <h3 className="text-sm font-semibold text-slate-900">Security Settings</h3>
              {["Two-Factor Authentication", "Session Timeout (30 mins)", "IP Allowlisting", "Audit Logging"].map(s => (
                <div key={s} className="flex items-center justify-between py-2 border-b border-slate-100 last:border-0">
                  <div>
                    <p className="text-sm font-medium text-slate-800">{s}</p>
                    <p className="text-xs text-slate-400">Enable this security feature</p>
                  </div>
                  <button className="w-10 h-6 rounded-full bg-blue-600 flex items-center px-0.5 relative">
                    <div className="w-4 h-4 bg-white rounded-full shadow translate-x-4 transition-transform" />
                  </button>
                </div>
              ))}
            </Card>
          )}
          {tab === "ai" && (
            <Card className="p-6 space-y-4">
              <h3 className="text-sm font-semibold text-slate-900">AI Models</h3>
              {[
                { name: "R2WAI LLM (Local)", status: "Active", type: "Default" },
                { name: "R2WAI LLM (Cloud)", status: "Active", type: "Cloud" },
                { name: "Custom Fine-tuned", status: "Inactive", type: "Custom" },
              ].map(m => (
                <div key={m.name} className="flex items-center justify-between p-3 rounded-lg border border-slate-200">
                  <div className="flex items-center gap-3">
                    <div className="w-8 h-8 rounded-lg bg-purple-50 flex items-center justify-center">
                      <Cpu className="w-4 h-4 text-purple-600" />
                    </div>
                    <div>
                      <p className="text-sm font-medium text-slate-900">{m.name}</p>
                      <p className="text-xs text-slate-400">{m.type}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    <StatusBadge status={m.status} />
                    <Btn variant="ghost" size="xs">Configure</Btn>
                  </div>
                </div>
              ))}
            </Card>
          )}
          {(tab === "email" || tab === "storage" || tab === "backup" || tab === "notifications") && (
            <Card className="p-6">
              <h3 className="text-sm font-semibold text-slate-900 mb-4 capitalize">{tab} Settings</h3>
              <div className="space-y-4">
                <FormField label="Service Provider">
                  <Select value="default" options={[{ value: "default", label: "Default" }]} className="w-full" />
                </FormField>
                <FormField label="Configuration">
                  <Input placeholder="Enter configuration value" />
                </FormField>
                <div className="flex items-center gap-2">
                  <input type="checkbox" defaultChecked className="rounded" />
                  <span className="text-sm text-slate-600">Enable service</span>
                </div>
                <div className="flex justify-end">
                  <Btn variant="primary" size="sm">Save Changes</Btn>
                </div>
              </div>
            </Card>
          )}
          {tab === "audit" && (
            <Card className="p-5">
              <h3 className="text-sm font-semibold text-slate-900 mb-4">Audit Logs</h3>
              <Table
                columns={[
                  { key: "time", label: "Time" },
                  { key: "user", label: "User" },
                  { key: "action", label: "Action" },
                  { key: "resource", label: "Resource" },
                ]}
                rows={[
                  { time: "12 May 09:30", user: "Super Admin", action: <Badge variant="blue">Created</Badge>, resource: "AI Assistant: Citizen Support" },
                  { time: "12 May 09:00", user: "Admin", action: <Badge variant="success">Updated</Badge>, resource: "Automation: Payment Confirmation" },
                  { time: "11 May 18:00", user: "Admin", action: <Badge variant="error">Deleted</Badge>, resource: "User: test@gov.in" },
                  { time: "11 May 14:00", user: "Super Admin", action: <Badge variant="blue">Published</Badge>, resource: "Automation: Welcome Notification" },
                ]}
              />
            </Card>
          )}
        </div>
      </div>
    </div>
  );
}

// ─── COMPONENT: Floating Chatbot ──────────────────────────────────────────────
function FloatingChatbot() {
  const [open, setOpen] = useState(false);
  const [msg, setMsg] = useState("");
  const [chat, setChat] = useState([
    { role: "assistant", text: "Hello! I'm the Citizen Support AI. How can I help you today?" },
  ]);

  function send() {
    if (!msg.trim()) return;
    setChat(c => [...c,
      { role: "user", text: msg },
      { role: "assistant", text: "Thank you for your query! Let me help you with that. Please provide your application number." }
    ]);
    setMsg("");
  }

  return (
    <div className="fixed bottom-5 right-5 z-50 flex flex-col items-end gap-3">
      {open && (
        <div className="w-80 bg-white rounded-2xl shadow-2xl border border-slate-200 overflow-hidden">
          {/* Header */}
          <div className="bg-blue-600 p-4 flex items-center gap-3">
            <div className="w-8 h-8 rounded-full bg-white/20 flex items-center justify-center">
              <Bot className="w-4 h-4 text-white" />
            </div>
            <div className="flex-1">
              <p className="text-sm font-semibold text-white">Citizen Support</p>
              <div className="flex items-center gap-1.5">
                <div className="w-1.5 h-1.5 rounded-full bg-green-400" />
                <p className="text-xs text-blue-200">Online</p>
              </div>
            </div>
            <button onClick={() => setOpen(false)} className="text-white/70 hover:text-white">
              <X className="w-4 h-4" />
            </button>
          </div>

          {/* Chat */}
          <div className="h-60 overflow-y-auto p-4 space-y-3 bg-slate-50">
            {chat.map((m, i) => (
              <div key={i} className={cn("flex gap-2", m.role === "user" && "justify-end")}>
                {m.role === "assistant" && (
                  <div className="w-6 h-6 rounded-full bg-blue-600 flex items-center justify-center shrink-0 mt-0.5">
                    <Bot className="w-3 h-3 text-white" />
                  </div>
                )}
                <div className={cn(
                  "max-w-[200px] rounded-xl px-3 py-2 text-xs",
                  m.role === "user" ? "bg-blue-600 text-white rounded-tr-sm" : "bg-white text-slate-700 shadow-sm rounded-tl-sm"
                )}>{m.text}</div>
              </div>
            ))}
          </div>

          {/* Input */}
          <div className="p-3 border-t border-slate-200 bg-white flex items-center gap-2">
            <input value={msg} onChange={e => setMsg(e.target.value)}
              onKeyDown={e => e.key === "Enter" && send()}
              placeholder="Type a message…"
              className="flex-1 text-xs border border-slate-200 rounded-xl px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500" />
            <button onClick={send} className="w-7 h-7 bg-blue-600 rounded-xl flex items-center justify-center hover:bg-blue-700">
              <Send className="w-3 h-3 text-white" />
            </button>
          </div>
          <div className="text-center py-2 text-[10px] text-slate-300">Powered by R2WAI</div>
        </div>
      )}

      <button onClick={() => setOpen(!open)}
        className="w-12 h-12 bg-blue-600 hover:bg-blue-700 rounded-full shadow-lg flex items-center justify-center transition-all hover:scale-110 active:scale-95">
        {open ? <X className="w-5 h-5 text-white" /> : <MessageSquare className="w-5 h-5 text-white" />}
      </button>
    </div>
  );
}

// ─── App ─────────────────────────────────────────────────────────────────────
export default function App() {
  const [screen, setScreen] = useState<Screen>("login");
  const [role, setRole] = useState<Role>("superAdmin");
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [drawer, setDrawer] = useState<Drawer>(null);

  const nav = (s: Screen) => setScreen(s);

  if (screen === "login") {
    return <LoginScreen onLogin={r => { setRole(r); setScreen("dashboard"); }} />;
  }

  // Screens that use the full canvas (no sidebar header chrome)
  const fullCanvas = screen === "workflowBuilder";

  function renderScreen() {
    switch (screen) {
      case "dashboard":
        return role === "user"
          ? <UserDashboard onNavigate={nav} />
          : role === "admin"
            ? <AdminDashboard onNavigate={nav} />
            : <SuperAdminDashboard onNavigate={nav} />;
      case "aiAssistants": return <AIAssistantsList onNavigate={nav} onOpenDrawer={setDrawer} />;
      case "aiDetail": return <AIAssistantDetail onNavigate={nav} onOpenDrawer={setDrawer} />;
      case "aiPlayground": return <AIAssistantPlayground onNavigate={nav} />;
      case "automations": return <AutomationsList onNavigate={nav} onOpenDrawer={setDrawer} />;
      case "automationWizard": return <NewAutomationWizard onNavigate={nav} />;
      case "workflowBuilder": return <AdvancedWorkflowBuilder onNavigate={nav} />;
      case "knowledge": return <KnowledgeList onNavigate={nav} />;
      case "knowledgeEdit": return <KnowledgeEditScreen onNavigate={nav} />;
      case "integrations": return <IntegrationsList onNavigate={nav} />;
      case "integrationEdit": return <IntegrationEditScreen onNavigate={nav} />;
      case "toolsApis": return <ToolsAPIs onNavigate={nav} />;
      case "testPlayground": return <AIAssistantPlayground onNavigate={nav} />;
      case "publish": return <PublishScreen onNavigate={nav} />;
      case "monitor": return <MonitorDashboard onNavigate={nav} />;
      case "executionDetails": return <ExecutionDetailsScreen onNavigate={nav} />;
      case "usersRoles": return <UsersRolesScreen onNavigate={nav} />;
      case "permissionsMatrix": return <PermissionsMatrixScreen onNavigate={nav} />;
      case "settings": return <SettingsScreen />;
      default: return <SuperAdminDashboard onNavigate={nav} />;
    }
  }

  return (
    <div className="h-screen flex bg-slate-50 overflow-hidden">
      <Sidebar
        screen={screen}
        role={role}
        onNavigate={nav}
        collapsed={sidebarCollapsed}
        onToggle={() => setSidebarCollapsed(!sidebarCollapsed)}
      />
      <div className="flex-1 flex flex-col min-w-0 overflow-hidden">
        {!fullCanvas && (
          <TopBar role={role} onRoleChange={r => { setRole(r); setScreen("dashboard"); }} onNavigate={nav} />
        )}
        <main className={cn("flex-1 overflow-y-auto", fullCanvas && "flex flex-col")}>
          {renderScreen()}
        </main>
      </div>

      {/* Drawers */}
      <AIAssistantEditDrawer open={drawer === "aiEdit"} onClose={() => setDrawer(null)} />
      <AutomationDetailDrawer
        open={drawer === "automationDetail"}
        onClose={() => setDrawer(null)}
        onNavigate={s => { setDrawer(null); nav(s); }}
      />

      {/* Floating chatbot (always visible in app) */}
      <FloatingChatbot />
    </div>
  );
}
