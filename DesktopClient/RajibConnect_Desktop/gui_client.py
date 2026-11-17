
import tkinter as tk
from tkinter import ttk, messagebox
import requests
import json
from urllib.parse import urlparse


# =====================================================
# RAJIB CONNECT DESKTOP CLIENT
# JWT Authentication + Analytics + Customer CRUD
# =====================================================

class RajibConnectClient:

    def __init__(self, root):

        self.root = root
        self.root.title("Rajib Connect - Desktop Client")
        self.root.geometry("1300x780")
        self.root.minsize(1000, 650)

        self.base_url = ""
        self.access_token = None
        self.username = ""
        self.roles = []

        self.session = requests.Session()
        self.configure_theme()
        self.module_nav_buttons = {}

        self.endpoints = {
            "Customers": "/api/customers",
            "Leads": "/api/leads",
            "Opportunities": "/api/opportunities",
            "Follow-ups": "/api/followups",
            "Activities": "/api/activities",
            "Audit Logs": "/api/auditlogs"
        }

        self.current_module = None
        self.current_records = []
        self.displayed_records = []

        self.show_login()

    # =================================================
    # GENERAL HELPERS
    # =================================================

    def configure_theme(self):
        """Apply a consistent modern blue-and-white desktop theme."""
        self.colors = {
            "navy": "#14213D", "navy_light": "#1E3155",
            "blue": "#2563EB", "blue_hover": "#1D4ED8",
            "blue_soft": "#EAF1FF", "background": "#F4F7FC",
            "surface": "#FFFFFF", "text": "#172033",
            "muted": "#718096", "border": "#E4EAF2",
            "green": "#0F9D78", "red": "#DC5353",
            "orange": "#D97706"
        }
        self.root.configure(bg=self.colors["background"])
        self.root.option_add("*Font", "{Segoe UI} 10")
        style = ttk.Style(self.root)
        try:
            style.theme_use("clam")
        except tk.TclError:
            pass
        style.configure("Treeview", background="#FFFFFF", foreground="#263247",
                        fieldbackground="#FFFFFF", rowheight=32, borderwidth=0,
                        font=("Segoe UI", 9))
        style.configure("Treeview.Heading", background="#EEF2F8", foreground="#34415A",
                        font=("Segoe UI", 9, "bold"), relief="flat", padding=(8, 9))
        style.map("Treeview", background=[("selected", "#DCE8FF")],
                  foreground=[("selected", "#14213D")])
        style.configure("Vertical.TScrollbar", background="#E8EDF5", troughcolor="#F7F9FC",
                        bordercolor="#F7F9FC", arrowsize=14)
        self.root.title("Rajib Connect | Desktop CRM")
        self.root.geometry("1380x850")
        self.root.minsize(1080, 700)

    def clear_window(self):
        for widget in self.root.winfo_children():
            widget.destroy()

    def is_admin_or_manager(self):

        return (
            "Admin" in self.roles
            or "Manager" in self.roles
        )

    def validate_server_url(self, url):

        parsed = urlparse(url)

        if not parsed.hostname:
            return False

        return (
            parsed.scheme == "https"
            or (
                parsed.scheme == "http"
                and parsed.hostname.lower()
                in ("localhost", "127.0.0.1", "::1")
            )
        )

    def show_api_error(self, response):

        try:
            data = response.json()

            if isinstance(data, dict):
                message = (
                    data.get("message")
                    or data.get("title")
                    or data.get("detail")
                    or json.dumps(data)
                )
            else:
                message = str(data)

        except ValueError:
            message = response.text or "Unknown server error."

        messagebox.showerror(
            f"API Error - HTTP {response.status_code}",
            message
        )

    # =================================================
    # LOGIN SCREEN
    # =================================================

    def show_login(self):
        self.clear_window()
        self.root.configure(bg="#F4F7FC")

        shell = tk.Frame(self.root, bg="#F4F7FC")
        shell.pack(fill="both", expand=True)

        left = tk.Frame(shell, bg="#14213D", width=520)
        left.pack(side="left", fill="y")
        left.pack_propagate(False)
        tk.Label(left, text="RC", font=("Segoe UI", 34, "bold"),
                 bg="#2563EB", fg="white", width=3, height=1).pack(anchor="w", padx=58, pady=(105, 24))
        tk.Label(left, text="Rajib Connect", font=("Segoe UI", 30, "bold"),
                 bg="#14213D", fg="white").pack(anchor="w", padx=58)
        tk.Label(left, text="A clearer way to manage\nyour customer relationships.",
                 font=("Segoe UI", 15), justify="left", bg="#14213D",
                 fg="#C6D2E5").pack(anchor="w", padx=58, pady=(14, 0))
        tk.Label(left, text="CUSTOMER RELATIONSHIP MANAGEMENT",
                 font=("Segoe UI", 8, "bold"), bg="#14213D", fg="#8FA5C7").pack(
                     side="bottom", anchor="w", padx=58, pady=35)

        right = tk.Frame(shell, bg="#F4F7FC")
        right.pack(side="right", fill="both", expand=True)
        card = tk.Frame(right, bg="white", padx=48, pady=42,
                        highlightbackground="#E4EAF2", highlightthickness=1)
        card.place(relx=0.5, rely=0.5, anchor="center")

        tk.Label(card, text="Welcome back", font=("Segoe UI", 25, "bold"),
                 bg="white", fg="#172033").pack(anchor="w")
        tk.Label(card, text="Sign in to continue to your workspace.",
                 font=("Segoe UI", 10), bg="white", fg="#718096").pack(anchor="w", pady=(5, 27))

        def field(label, show=None):
            tk.Label(card, text=label, font=("Segoe UI", 9, "bold"),
                     bg="white", fg="#34415A").pack(anchor="w", pady=(9, 6))
            entry = tk.Entry(card, width=39, font=("Segoe UI", 11), relief="flat",
                             bg="#F5F7FB", fg="#172033", insertbackground="#172033",
                             highlightthickness=1, highlightbackground="#E4EAF2",
                             highlightcolor="#2563EB", show=show)
            entry.pack(fill="x", ipady=10)
            return entry

        self.url_entry = field("CRM SERVER URL")
        self.url_entry.insert(0, "http://localhost:5184")
        self.username_entry = field("USERNAME OR EMAIL")
        self.password_entry = field("PASSWORD", show="*")

        self.login_button = tk.Button(card, text="Sign in  →", command=self.login,
            bg="#2563EB", activebackground="#1D4ED8", activeforeground="white",
            fg="white", font=("Segoe UI", 11, "bold"), relief="flat",
            cursor="hand2", pady=12)
        self.login_button.pack(fill="x", pady=(25, 0))
        tk.Label(card, text="Protected with JWT authentication",
                 font=("Segoe UI", 9), bg="white", fg="#8A96A8").pack(pady=(18, 0))
        self.password_entry.bind("<Return>", lambda event: self.login())
        self.username_entry.focus_set()

    def login(self):

        self.base_url = (
            self.url_entry.get().strip().rstrip("/")
        )

        username = self.username_entry.get().strip()
        password = self.password_entry.get()

        if not self.base_url or not username or not password:

            messagebox.showwarning(
                "Missing Information",
                "Please complete all login fields."
            )
            return

        if not self.validate_server_url(self.base_url):

            messagebox.showwarning(
                "Invalid URL",
                "Use HTTPS or HTTP for localhost development."
            )
            return

        parsed = urlparse(self.base_url)

        is_localhost = parsed.hostname.lower() in (
            "localhost", "127.0.0.1", "::1"
        )

        self.session.verify = not is_localhost

        self.login_button.config(
            state="disabled",
            text="CONNECTING..."
        )

        self.root.update_idletasks()

        try:

            response = self.session.post(
                f"{self.base_url}/api/auth/login",
                json={
                    "userName": username,
                    "password": password
                },
                timeout=15
            )

            if response.status_code == 200:

                result = response.json()

                self.access_token = result["accessToken"]

                user = result.get("user", {})

                self.username = user.get(
                    "userName", username
                )

                self.roles = user.get("roles", [])

                self.password_entry.delete(0, tk.END)

                self.show_dashboard()

            elif response.status_code == 401:

                messagebox.showerror(
                    "Login Failed",
                    "Invalid credentials or inactive account."
                )

            elif response.status_code == 429:

                messagebox.showerror(
                    "Rate Limit",
                    "Too many login attempts. Please wait."
                )

            else:
                self.show_api_error(response)

        except requests.exceptions.RequestException as error:

            messagebox.showerror(
                "Connection Error",
                str(error)
            )

        except (ValueError, KeyError):

            messagebox.showerror(
                "Response Error",
                "Unexpected login response."
            )

        finally:

            try:
                if self.login_button.winfo_exists():
                    self.login_button.config(
                        state="normal",
                        text="LOGIN"
                    )
            except tk.TclError:
                pass

    # =================================================
    # AUTHENTICATED API METHODS
    # =================================================

    def api_request(self, method, endpoint, **kwargs):

        headers = kwargs.pop("headers", {})

        headers["Authorization"] = (
            f"Bearer {self.access_token}"
        )

        headers["Accept"] = "application/json"

        if "json" in kwargs:
            headers["Content-Type"] = "application/json"

        response = self.session.request(
            method,
            self.base_url + endpoint,
            headers=headers,
            timeout=20,
            **kwargs
        )

        if response.status_code == 401:

            messagebox.showerror(
                "Authentication Failed",
                "Your token is invalid, expired or rejected. "
                "Please log in again."
            )

            self.logout()
            return None

        return response

    def extract_records(self, data):

        if isinstance(data, list):
            return data

        if isinstance(data, dict):

            for key in ("items", "data", "results", "records"):

                if isinstance(data.get(key), list):
                    return data[key]

            return [data]

        return []

    # =================================================
    # DASHBOARD LAYOUT
    # =================================================

    def show_dashboard(self):
        self.clear_window()
        self.root.configure(bg=self.colors["background"])
        self.module_nav_buttons = {}

        header = tk.Frame(self.root, bg="#FFFFFF", height=72,
                          highlightbackground="#E4EAF2", highlightthickness=1)
        header.pack(fill="x")
        header.pack_propagate(False)
        brand = tk.Frame(header, bg="white")
        brand.pack(side="left", padx=25)
        tk.Label(brand, text="RC", bg="#2563EB", fg="white",
                 font=("Segoe UI", 13, "bold"), width=3, pady=5).pack(side="left", padx=(0, 11))
        tk.Label(brand, text="Rajib Connect", bg="white", fg="#172033",
                 font=("Segoe UI", 16, "bold")).pack(side="left")
        tk.Label(header, text=f"{self.username}   •   {' / '.join(self.roles)}",
                 bg="white", fg="#718096", font=("Segoe UI", 9)).pack(side="right", padx=25)

        body = tk.Frame(self.root, bg=self.colors["background"])
        body.pack(fill="both", expand=True)
        sidebar = tk.Frame(body, bg="#14213D", width=235)
        sidebar.pack(side="left", fill="y")
        sidebar.pack_propagate(False)
        tk.Label(sidebar, text="WORKSPACE", bg="#14213D", fg="#91A4C4",
                 font=("Segoe UI", 8, "bold")).pack(anchor="w", padx=22, pady=(27, 12))
        self.add_nav_button(sidebar, "Overview", self.show_welcome, "")
        tk.Label(sidebar, text="MANAGEMENT", bg="#14213D", fg="#91A4C4",
                 font=("Segoe UI", 8, "bold")).pack(anchor="w", padx=22, pady=(25, 10))
        for name in self.endpoints:
            if name == "Audit Logs" and "Admin" not in self.roles:
                continue
            self.add_nav_button(sidebar, name, lambda n=name: self.fetch_data(n), "")

        bottom = tk.Frame(sidebar, bg="#14213D")
        bottom.pack(side="bottom", fill="x", padx=14, pady=18)
        tk.Button(bottom, text="↪   Sign out", command=self.logout,
                  bg="#263B60", activebackground="#344D75", fg="#FFFFFF",
                  activeforeground="white", relief="flat", anchor="w",
                  font=("Segoe UI", 10, "bold"), cursor="hand2", padx=14,
                  pady=11).pack(fill="x")

        right = tk.Frame(body, bg=self.colors["background"])
        right.pack(side="right", fill="both", expand=True)
        self.content = tk.Frame(right, bg=self.colors["background"])
        self.content.pack(fill="both", expand=True, padx=26, pady=24)
        self.status_bar = tk.Label(right, text="Connected to CRM API  •  Secure session",
                                   bg="#FFFFFF", fg="#718096", anchor="w",
                                   font=("Segoe UI", 8), padx=18, pady=8)
        self.status_bar.pack(fill="x", side="bottom")
        self.show_welcome()

    def add_nav_button(self, parent, name, command, background=None):
        """Create and register a consistently styled sidebar navigation button."""
        button = tk.Button(
            parent,
            text="   " + name,
            command=command,
            bg="#14213D",
            activebackground="#263B60",
            fg="#D7E1F1",
            activeforeground="white",
            font=("Segoe UI", 10),
            relief="flat",
            anchor="w",
            cursor="hand2",
            padx=12,
            pady=11,
            bd=0,
            highlightthickness=0
        )
        button.pack(fill="x", padx=12, pady=2)
        self.module_nav_buttons[name] = button
        return button

    def set_active_nav(self, active):
        """Highlight the currently selected navigation item."""
        for name, button in self.module_nav_buttons.items():
            if name == active:
                button.configure(
                    bg="#2563EB",
                    fg="white",
                    activebackground="#1D4ED8"
                )
            else:
                button.configure(
                    bg="#14213D",
                    fg="#D7E1F1",
                    activebackground="#263B60"
                )

    # =================================================
    # ANALYTICS DASHBOARD
    # =================================================

    def show_welcome(self):
        self.set_active_nav("Overview")
        for widget in self.content.winfo_children():
            widget.destroy()
        heading = tk.Frame(self.content, bg=self.colors["background"])
        heading.pack(fill="x", pady=(0, 17))
        title_area = tk.Frame(heading, bg=self.colors["background"])
        title_area.pack(side="left")
        tk.Label(title_area, text="Good to see you, " + (self.username or "there"),
                 font=("Segoe UI", 23, "bold"), bg=self.colors["background"],
                 fg="#172033").pack(anchor="w")
        tk.Label(title_area, text="Here is what's happening across your CRM today.",
                 font=("Segoe UI", 10), bg=self.colors["background"],
                 fg="#718096").pack(anchor="w", pady=(4, 0))
        tk.Button(heading, text="↻  Refresh data", command=self.load_dashboard,
                  bg="#2563EB", activebackground="#1D4ED8", fg="white",
                  activeforeground="white", relief="flat", cursor="hand2",
                  font=("Segoe UI", 9, "bold"), padx=17, pady=10).pack(side="right", pady=5)
        self.dashboard_area = tk.Frame(self.content, bg=self.colors["background"])
        self.dashboard_area.pack(fill="both", expand=True)
        self.load_dashboard()

    def load_dashboard(self):

        for widget in self.dashboard_area.winfo_children():
            widget.destroy()

        tk.Label(
            self.dashboard_area,
            text="Loading CRM analytics...",
            bg=self.colors["background"],
            fg="#64748b"
        ).pack(pady=30)

        self.root.update_idletasks()

        results = {}

        try:

            for name in ("Customers", "Leads", "Opportunities"):

                response = self.api_request(
                    "GET",
                    self.endpoints[name]
                )

                if response is None:
                    return

                if not response.ok:
                    self.show_api_error(response)
                    return

                results[name] = self.extract_records(
                    response.json()
                )

        except (requests.exceptions.RequestException, ValueError) as error:
            for widget in self.dashboard_area.winfo_children():
                widget.destroy()
            tk.Label(
                self.dashboard_area,
                text="Dashboard data could not be loaded",
                font=("Segoe UI", 16, "bold"),
                bg=self.colors["background"],
                fg="#172033"
            ).pack(anchor="w", pady=(24, 8))
            tk.Label(
                self.dashboard_area,
                text=str(error),
                font=("Segoe UI", 10),
                bg=self.colors["background"],
                fg="#718096",
                wraplength=850,
                justify="left"
            ).pack(anchor="w")
            messagebox.showerror("Dashboard Error", str(error))
            return

        self.render_analytics(
            results["Customers"],
            results["Leads"],
            results["Opportunities"]
        )

    def create_stat_card(self, parent, title, value, color):
        card = tk.Frame(parent, bg="#FFFFFF", padx=19, pady=18,
                        highlightbackground="#E4EAF2", highlightthickness=1)
        card.pack(side="left", fill="both", expand=True, padx=(0, 12), pady=4)
        tk.Frame(card, bg=color, width=4, height=38).pack(side="left", fill="y", padx=(0, 13))
        text_box = tk.Frame(card, bg="white")
        text_box.pack(side="left", fill="both", expand=True)
        tk.Label(text_box, text=title, font=("Segoe UI", 8, "bold"),
                 bg="white", fg="#718096").pack(anchor="w")
        tk.Label(text_box, text=value, font=("Segoe UI", 21, "bold"),
                 bg="white", fg="#172033").pack(anchor="w", pady=(7, 0))

    def draw_bar_chart(self, parent, title, data):
        frame = tk.Frame(parent, bg="white", highlightbackground="#E4EAF2",
                         highlightthickness=1, padx=16, pady=14)
        frame.pack(side="left", fill="both", expand=True, padx=(0, 12), pady=5)
        tk.Label(frame, text=title, font=("Segoe UI", 11, "bold"),
                 bg="white", fg="#172033").pack(anchor="w", pady=(0, 12))
        canvas = tk.Canvas(frame, height=255, bg="white", highlightthickness=0)
        canvas.pack(fill="both", expand=True)
        canvas.update_idletasks()
        width = max(canvas.winfo_width(), 300)
        if not data:
            canvas.create_text(width / 2, 115, text="No records available yet",
                               fill="#8A96A8", font=("Segoe UI", 10))
            return
        maximum = max(data.values()) or 1
        top, bottom = 25, 190
        gap = width / (len(data) + 1)
        bar_width = min(42, max(18, int(width / (len(data) * 3))))
        colors = ["#2563EB", "#0F9D78", "#8B5CF6", "#D97706", "#0891B2", "#DB5A83"]
        for index, (label, value) in enumerate(data.items()):
            x = gap * (index + 1)
            bar_height = max(2, value / maximum * (bottom - top))
            y = bottom - bar_height
            canvas.create_rectangle(x-bar_width/2, y, x+bar_width/2, bottom,
                                    fill=colors[index % len(colors)], outline="", width=0)
            canvas.create_text(x, y-12, text=str(value), fill="#34415A",
                               font=("Segoe UI", 9, "bold"))
            canvas.create_text(x, bottom+23, text=str(label)[:14], fill="#718096",
                               font=("Segoe UI", 8), width=gap)
        canvas.create_line(8, bottom, width-8, bottom, fill="#E4EAF2")

    def render_analytics(self, customers, leads, opportunities):

        for widget in self.dashboard_area.winfo_children():
            widget.destroy()

        pipeline = 0.0

        for item in opportunities:

            stage = str(
                item.get("stage") or ""
            ).strip().lower()

            if stage in ("won", "lost"):
                continue

            amount = float(item.get("amount") or 0)
            probability = float(item.get("probability") or 0)

            pipeline += amount * probability / 100

        cards = tk.Frame(
            self.dashboard_area,
            bg="white"
        )

        cards.pack(fill="x", pady=8)

        self.create_stat_card(
            cards, "TOTAL CUSTOMERS",
            str(len(customers)), "#2563eb"
        )

        self.create_stat_card(
            cards, "TOTAL LEADS",
            str(len(leads)), "#0d9488"
        )

        self.create_stat_card(
            cards, "OPPORTUNITIES",
            str(len(opportunities)), "#7c3aed"
        )

        self.create_stat_card(
            cards, "ACTIVE WEIGHTED PIPELINE",
            f"₹{pipeline:,.2f}", "#ea580c"
        )

        lead_status = {}

        for item in leads:

            status = str(item.get("status") or "Unknown")

            lead_status[status] = (
                lead_status.get(status, 0) + 1
            )

        opportunity_stages = {}

        for item in opportunities:

            stage = str(item.get("stage") or "Unknown")

            opportunity_stages[stage] = (
                opportunity_stages.get(stage, 0) + 1
            )

        charts = tk.Frame(
            self.dashboard_area,
            bg="white"
        )

        charts.pack(
            fill="both",
            expand=True,
            pady=10
        )

        self.draw_bar_chart(
            charts,
            "Lead Status Distribution",
            lead_status
        )

        self.draw_bar_chart(
            charts,
            "Opportunity Stage Distribution",
            opportunity_stages
        )

        tk.Label(
            self.dashboard_area,
            text=(
                "Live CRM API data. Weighted pipeline = Amount × Probability / 100. "
                "Won and Lost opportunities excluded."
            ),
            bg="white",
            fg="#64748b",
            font=("Arial", 9),
            wraplength=850
        ).pack(anchor="w", pady=8)

    # =================================================
    # FETCH MODULE DATA
    # =================================================

    def fetch_data(self, module):

        self.current_module = module
        self.set_active_nav(module)

        try:

            if module == "Audit Logs":
                response = self.api_request("GET", self.endpoints[module], params={"page": 1, "pageSize": 100})
            else:
                response = self.api_request("GET", self.endpoints[module])

            if response is None:
                return

            if response.status_code == 200:

                self.current_records = self.extract_records(
                    response.json()
                )

                if module == "Customers":
                    self.display_customer_table()
                elif module == "Leads":
                    self.display_leads_table()
                else:
                    self.display_generic_table(module)

            else:
                self.show_api_error(response)

        except (requests.exceptions.RequestException, ValueError) as error:

            messagebox.showerror(
                "API Error",
                str(error)
            )

    # =================================================
    # CUSTOMER MANAGEMENT TABLE
    # =================================================

    def display_customer_table(self):

        for widget in self.content.winfo_children():
            widget.destroy()

        heading = tk.Frame(
            self.content,
            bg="white"
        )

        heading.pack(
            fill="x",
            padx=20,
            pady=(18, 10)
        )

        tk.Label(
            heading,
            text="Customer Management",
            font=("Arial", 22, "bold"),
            bg="white",
            fg="#1e3a8a"
        ).pack(side="left")

        tk.Button(
            heading,
            text="+ ADD CUSTOMER",
            command=lambda: self.customer_form(),
            bg="#2563eb",
            fg="white",
            relief="flat",
            padx=12,
            pady=8
        ).pack(side="right")

        toolbar = tk.Frame(
            self.content,
            bg="white"
        )

        toolbar.pack(
            fill="x",
            padx=20,
            pady=8
        )

        tk.Label(
            toolbar,
            text="Search:",
            bg="white",
            font=("Arial", 10, "bold")
        ).pack(side="left")

        self.customer_search = tk.Entry(
            toolbar,
            width=32,
            font=("Arial", 10)
        )

        self.customer_search.pack(
            side="left",
            padx=10,
            ipady=5
        )

        self.customer_search.bind(
            "<KeyRelease>",
            self.search_customers
        )

        tk.Button(
            toolbar,
            text="REFRESH",
            command=self.refresh_customers,
            bg="#e2e8f0",
            fg="#1e293b",
            relief="flat",
            padx=12,
            pady=6
        ).pack(side="right")

        self.customer_count = tk.Label(
            toolbar,
            text="",
            bg="white",
            fg="#64748b"
        )

        self.customer_count.pack(side="right", padx=15)

        # Table

        table_frame = tk.Frame(
            self.content,
            bg="white"
        )

        table_frame.pack(
            fill="both",
            expand=True,
            padx=20,
            pady=10
        )

        columns = (
            "customerId",
            "customerCode",
            "customerName",
            "email",
            "phone",
            "companyName",
            "city",
            "state",
            "status",
            "ownerName"
        )

        self.customer_tree = ttk.Treeview(
            table_frame,
            columns=columns,
            show="headings",
            selectmode="browse"
        )

        headings = {
            "customerId": "ID",
            "customerCode": "Customer Code",
            "customerName": "Customer Name",
            "email": "Email",
            "phone": "Phone",
            "companyName": "Company",
            "city": "City",
            "state": "State",
            "status": "Status",
            "ownerName": "Owner"
        }

        widths = {
            "customerId": 55,
            "customerCode": 110,
            "customerName": 150,
            "email": 180,
            "phone": 110,
            "companyName": 140,
            "city": 100,
            "state": 100,
            "status": 85,
            "ownerName": 130
        }

        for column in columns:

            self.customer_tree.heading(
                column,
                text=headings[column]
            )

            self.customer_tree.column(
                column,
                width=widths[column],
                minwidth=60,
                anchor="w"
            )

        vertical = ttk.Scrollbar(
            table_frame,
            orient="vertical",
            command=self.customer_tree.yview
        )

        horizontal = ttk.Scrollbar(
            table_frame,
            orient="horizontal",
            command=self.customer_tree.xview
        )

        self.customer_tree.configure(
            yscrollcommand=vertical.set,
            xscrollcommand=horizontal.set
        )

        vertical.pack(side="right", fill="y")
        horizontal.pack(side="bottom", fill="x")

        self.customer_tree.pack(
            side="left",
            fill="both",
            expand=True
        )

        self.customer_tree.bind(
            "<Double-1>",
            lambda event: self.view_customer()
        )

        # Action buttons

        actions = tk.Frame(
            self.content,
            bg="white"
        )

        actions.pack(
            fill="x",
            padx=20,
            pady=(0, 15)
        )

        tk.Button(
            actions,
            text="VIEW DETAILS",
            command=self.view_customer,
            bg="#e0f2fe",
            fg="#075985",
            relief="flat",
            padx=12,
            pady=8
        ).pack(side="left", padx=4)

        tk.Button(
            actions,
            text="EDIT CUSTOMER",
            command=self.edit_customer,
            bg="#fef3c7",
            fg="#92400e",
            relief="flat",
            padx=12,
            pady=8
        ).pack(side="left", padx=4)

        if self.is_admin_or_manager():

            tk.Button(
                actions,
                text="DELETE CUSTOMER",
                command=self.delete_customer,
                bg="#fee2e2",
                fg="#b91c1c",
                relief="flat",
                padx=12,
                pady=8
            ).pack(side="left", padx=4)

        self.populate_customer_table(
            self.current_records
        )

    # =================================================
    # POPULATE CUSTOMER TABLE
    # =================================================

    def populate_customer_table(self, records):

        for item in self.customer_tree.get_children():
            self.customer_tree.delete(item)

        self.displayed_records = records

        columns = self.customer_tree["columns"]

        for record in records:

            values = [
                record.get(column, "")
                for column in columns
            ]

            self.customer_tree.insert(
                "",
                "end",
                values=values
            )

        self.customer_count.config(
            text=f"{len(records)} customer(s)"
        )

    # =================================================
    # SEARCH CUSTOMERS
    # =================================================

    def search_customers(self, event=None):

        query = (
            self.customer_search.get()
            .strip()
            .lower()
        )

        if not query:

            self.populate_customer_table(
                self.current_records
            )

            return

        filtered = []

        for customer in self.current_records:

            searchable = " ".join(
                str(customer.get(field, ""))
                for field in (
                    "customerCode",
                    "customerName",
                    "email",
                    "companyName",
                    "phone",
                    "city",
                    "state",
                    "ownerName"
                )
            ).lower()

            if query in searchable:
                filtered.append(customer)

        self.populate_customer_table(filtered)

    # =================================================
    # REFRESH CUSTOMERS
    # =================================================

    def refresh_customers(self):

        response = self.api_request(
            "GET",
            "/api/customers"
        )

        if response is None:
            return

        if response.status_code == 200:

            self.current_records = self.extract_records(
                response.json()
            )

            self.customer_search.delete(0, tk.END)

            self.populate_customer_table(
                self.current_records
            )

        else:
            self.show_api_error(response)

    # =================================================
    # SELECT CUSTOMER
    # =================================================

    def get_selected_customer(self):

        selected = self.customer_tree.selection()

        if not selected:

            messagebox.showwarning(
                "No Selection",
                "Please select a customer first."
            )

            return None

        index = self.customer_tree.index(selected[0])

        if index >= len(self.displayed_records):
            return None

        return self.displayed_records[index]

    # =================================================
    # VIEW CUSTOMER
    # =================================================

    def view_customer(self):

        customer = self.get_selected_customer()

        if not customer:
            return

        customer_id = customer.get("customerId")

        response = self.api_request(
            "GET",
            f"/api/customers/{customer_id}"
        )

        if response is None:
            return

        if response.status_code != 200:

            self.show_api_error(response)
            return

        data = response.json()

        details = tk.Toplevel(self.root)

        details.title("Customer Details")
        details.geometry("600x600")
        details.transient(self.root)

        tk.Label(
            details,
            text="CUSTOMER PROFILE",
            font=("Arial", 19, "bold"),
            fg="#1e3a8a"
        ).pack(pady=15)

        frame = tk.Frame(details)
        frame.pack(fill="both", expand=True, padx=25, pady=10)

        fields = [
            ("Customer ID", "customerId"),
            ("Customer Code", "customerCode"),
            ("Customer Name", "customerName"),
            ("Email", "email"),
            ("Phone", "phone"),
            ("Company", "companyName"),
            ("Address", "address"),
            ("City", "city"),
            ("State", "state"),
            ("Status", "status"),
            ("Owner", "ownerName"),
            ("Created Date", "createdDate"),
            ("Modified Date", "modifiedDate")
        ]

        for label, key in fields:

            row = tk.Frame(frame)
            row.pack(fill="x", pady=5)

            tk.Label(
                row,
                text=label + ":",
                width=17,
                anchor="w",
                font=("Arial", 9, "bold")
            ).pack(side="left")

            tk.Label(
                row,
                text=str(data.get(key) or "-"),
                anchor="w",
                wraplength=330
            ).pack(side="left", fill="x")

        tk.Button(
            details,
            text="CLOSE",
            command=details.destroy,
            bg="#1e3a8a",
            fg="white",
            relief="flat",
            padx=20,
            pady=7
        ).pack(pady=15)

    # =================================================
    # ADD / EDIT CUSTOMER FORM
    # =================================================

    def customer_form(self, customer=None):

        is_edit = customer is not None

        form = tk.Toplevel(self.root)

        form.title(
            "Edit Customer" if is_edit else "Add Customer"
        )

        form.geometry("600x700")
        form.minsize(550, 650)
        form.transient(self.root)

        form.grab_set()

        tk.Label(
            form,
            text=(
                "EDIT CUSTOMER"
                if is_edit else "ADD NEW CUSTOMER"
            ),
            font=("Arial", 19, "bold"),
            fg="#1e3a8a"
        ).pack(pady=15)

        container = tk.Frame(form)
        container.pack(
            fill="both",
            expand=True,
            padx=30,
            pady=5
        )

        entries = {}

        fields = [
            ("Customer Code", "customerCode"),
            ("Customer Name", "customerName"),
            ("Email", "email"),
            ("Phone", "phone"),
            ("Company Name", "companyName"),
            ("Address", "address"),
            ("City", "city"),
            ("State", "state")
        ]

        for label, key in fields:

            row = tk.Frame(container)
            row.pack(fill="x", pady=6)

            tk.Label(
                row,
                text=label,
                width=17,
                anchor="w",
                font=("Arial", 9, "bold")
            ).pack(side="left")

            entry = tk.Entry(
                row,
                font=("Arial", 10),
                width=40
            )

            entry.pack(
                side="right",
                fill="x",
                expand=True,
                ipady=4
            )

            if is_edit:
                entry.insert(
                    0,
                    customer.get(key) or ""
                )

            entries[key] = entry

        # Status is editable only when updating.

        if is_edit:

            row = tk.Frame(container)
            row.pack(fill="x", pady=6)

            tk.Label(
                row,
                text="Status",
                width=17,
                anchor="w",
                font=("Arial", 9, "bold")
            ).pack(side="left")

            status_box = ttk.Combobox(
                row,
                values=["Active", "Inactive"],
                state="readonly",
                width=37
            )

            status_box.set(
                customer.get("status", "Active")
            )

            status_box.pack(side="right", fill="x", expand=True)

        else:
            status_box = None

        # Ownership field is available only to Admin/Manager.

        owner_entry = None

        if self.is_admin_or_manager():

            row = tk.Frame(container)
            row.pack(fill="x", pady=6)

            tk.Label(
                row,
                text="Owner ID",
                width=17,
                anchor="w",
                font=("Arial", 9, "bold")
            ).pack(side="left")

            owner_entry = tk.Entry(
                row,
                font=("Arial", 10),
                width=40
            )

            owner_entry.pack(
                side="right",
                fill="x",
                expand=True,
                ipady=4
            )

            if is_edit:
                owner_entry.insert(
                    0,
                    customer.get("ownerId") or ""
                )

            tk.Label(
                container,
                text=(
                    "Enter the Identity ID of an active Sales Executive. "
                    "Leave blank for unassigned."
                ),
                fg="#64748b",
                font=("Arial", 8),
                wraplength=490,
                justify="left"
            ).pack(anchor="w", pady=5)

        tk.Label(
            container,
            text=(
                "All fields are submitted to the CRM API for validation. "
                "New customers are created with Active status."
            ),
            fg="#64748b",
            font=("Arial", 9),
            wraplength=490,
            justify="left"
        ).pack(anchor="w", pady=10)

        # Submit

        def save_customer():

            values = {}

            for _, key in fields:

                value = entries[key].get().strip()

                if not value:

                    if key in (
                        "companyName",
                        "address",
                        "city",
                        "state"
                    ):
                        value = ""

                    else:

                        messagebox.showwarning(
                            "Required Field",
                            f"Please enter {key.replace('Name', ' name')}."
                        )

                        entries[key].focus_set()
                        return

                values[key] = value

            payload = {
                "customerCode": values["customerCode"],
                "customerName": values["customerName"],
                "email": values["email"],
                "phone": values["phone"],
                "companyName": values["companyName"],
                "address": values["address"],
                "city": values["city"],
                "state": values["state"],
                "status": (
                    status_box.get()
                    if is_edit else "Active"
                )
            }

            if owner_entry is not None:

                owner_id = owner_entry.get().strip()

                payload["ownerId"] = owner_id or None

            if is_edit:

                endpoint = (
                    f"/api/customers/{customer['customerId']}"
                )

                method = "PUT"

            else:

                endpoint = "/api/customers"
                method = "POST"

            try:

                response = self.api_request(
                    method,
                    endpoint,
                    json=payload
                )

                if response is None:
                    return

                if response.status_code in (200, 201):

                    messagebox.showinfo(
                        "Success",
                        (
                            "Customer updated successfully."
                            if is_edit
                            else "Customer created successfully."
                        )
                    )

                    form.destroy()

                    self.refresh_customers()

                else:

                    self.show_api_error(response)

            except requests.exceptions.RequestException as error:

                messagebox.showerror(
                    "Request Error",
                    str(error)
                )

        buttons = tk.Frame(form)
        buttons.pack(pady=20)

        tk.Button(
            buttons,
            text="SAVE CUSTOMER",
            command=save_customer,
            bg="#2563eb",
            fg="white",
            font=("Arial", 10, "bold"),
            relief="flat",
            padx=20,
            pady=9
        ).pack(side="left", padx=8)

        tk.Button(
            buttons,
            text="CANCEL",
            command=form.destroy,
            bg="#e2e8f0",
            fg="#334155",
            relief="flat",
            padx=20,
            pady=9
        ).pack(side="left", padx=8)

    # =================================================
    # EDIT CUSTOMER
    # =================================================

    def edit_customer(self):

        customer = self.get_selected_customer()

        if not customer:
            return

        self.customer_form(customer)

    # =================================================
    # DELETE CUSTOMER
    # =================================================

    def delete_customer(self):

        if not self.is_admin_or_manager():

            messagebox.showerror(
                "Access Denied",
                "Only Admin and Manager can delete customers."
            )

            return

        customer = self.get_selected_customer()

        if not customer:
            return

        name = customer.get("customerName", "this customer")

        confirmed = messagebox.askyesno(
            "Confirm Deletion",
            (
                f"Are you sure you want to delete {name}?\n\n"
                "This operation cannot be undone."
            )
        )

        if not confirmed:
            return

        customer_id = customer.get("customerId")

        try:

            response = self.api_request(
                "DELETE",
                f"/api/customers/{customer_id}"
            )

            if response is None:
                return

            if response.status_code in (200, 204):

                messagebox.showinfo(
                    "Success",
                    "Customer deleted successfully."
                )

                self.refresh_customers()

            else:

                self.show_api_error(response)

        except requests.exceptions.RequestException as error:

            messagebox.showerror(
                "Delete Error",
                str(error)
            )

    # =================================================
    # LEAD MANAGEMENT - FULL CRUD
    # =================================================

    def display_leads_table(self):
        """Render lead management with search, filters and role-aware actions."""
        for widget in self.content.winfo_children():
            widget.destroy()

        self.set_active_nav("Leads")
        outer = tk.Frame(self.content, bg=self.colors["background"])
        outer.pack(fill="both", expand=True)

        heading = tk.Frame(outer, bg=self.colors["background"])
        heading.pack(fill="x", pady=(0, 16))
        tk.Label(heading, text="Lead Management", font=("Segoe UI", 22, "bold"),
                 bg=self.colors["background"], fg=self.colors["text"]).pack(side="left")
        tk.Button(heading, text="+  Add Lead", command=self.create_lead,
                  bg=self.colors["blue"], fg="white", activebackground=self.colors["blue_hover"],
                  activeforeground="white", relief="flat", font=("Segoe UI", 10, "bold"),
                  padx=18, pady=9, cursor="hand2").pack(side="right")
        tk.Button(heading, text="Refresh", command=lambda: self.fetch_data("Leads"),
                  bg="white", fg=self.colors["text"], relief="flat",
                  font=("Segoe UI", 9, "bold"), padx=15, pady=9,
                  cursor="hand2").pack(side="right", padx=(0, 8))

        filter_card = tk.Frame(outer, bg="white", padx=14, pady=13,
                               highlightbackground=self.colors["border"], highlightthickness=1)
        filter_card.pack(fill="x", pady=(0, 12))
        tk.Label(filter_card, text="Search", bg="white", fg=self.colors["muted"],
                 font=("Segoe UI", 9, "bold")).grid(row=0, column=0, sticky="w", padx=(0, 7))
        self.lead_search_entry = tk.Entry(filter_card, width=32, relief="solid", bd=1,
                                          font=("Segoe UI", 10))
        self.lead_search_entry.grid(row=0, column=1, sticky="w", padx=(0, 18), ipady=6)
        self.lead_search_entry.bind("<KeyRelease>", lambda e: self.filter_leads())

        tk.Label(filter_card, text="Status", bg="white", fg=self.colors["muted"],
                 font=("Segoe UI", 9, "bold")).grid(row=0, column=2, sticky="w", padx=(0, 7))
        self.lead_status_filter = ttk.Combobox(filter_card, state="readonly", width=16,
            values=["All", "New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost"])
        self.lead_status_filter.set("All")
        self.lead_status_filter.grid(row=0, column=3, padx=(0, 18), ipady=4)
        self.lead_status_filter.bind("<<ComboboxSelected>>", lambda e: self.filter_leads())

        tk.Label(filter_card, text="Priority", bg="white", fg=self.colors["muted"],
                 font=("Segoe UI", 9, "bold")).grid(row=0, column=4, sticky="w", padx=(0, 7))
        self.lead_priority_filter = ttk.Combobox(filter_card, state="readonly", width=12,
                                                  values=["All", "Low", "Medium", "High"])
        self.lead_priority_filter.set("All")
        self.lead_priority_filter.grid(row=0, column=5, padx=(0, 8), ipady=4)
        self.lead_priority_filter.bind("<<ComboboxSelected>>", lambda e: self.filter_leads())

        table_card = tk.Frame(outer, bg="white", highlightbackground=self.colors["border"],
                              highlightthickness=1)
        table_card.pack(fill="both", expand=True)
        columns = ("leadCode", "leadName", "companyName", "source", "status",
                   "priority", "expectedValue", "assignedToName", "createdDate")
        self.lead_tree = ttk.Treeview(table_card, columns=columns, show="headings", selectmode="browse")
        headings = {
            "leadCode": ("Lead Code", 105), "leadName": ("Lead Name", 165),
            "companyName": ("Company", 145), "source": ("Source", 115),
            "status": ("Status", 105), "priority": ("Priority", 85),
            "expectedValue": ("Expected Value", 115),
            "assignedToName": ("Assigned To", 145), "createdDate": ("Created", 145)
        }
        for key, (label, width) in headings.items():
            self.lead_tree.heading(key, text=label)
            self.lead_tree.column(key, width=width, minwidth=75, anchor="w")

        ybar = ttk.Scrollbar(table_card, orient="vertical", command=self.lead_tree.yview)
        xbar = ttk.Scrollbar(table_card, orient="horizontal", command=self.lead_tree.xview)
        self.lead_tree.configure(yscrollcommand=ybar.set, xscrollcommand=xbar.set)
        ybar.pack(side="right", fill="y")
        xbar.pack(side="bottom", fill="x")
        self.lead_tree.pack(fill="both", expand=True, padx=8, pady=8)
        self.lead_tree.bind("<Double-1>", lambda e: self.view_lead())

        actions = tk.Frame(outer, bg=self.colors["background"])
        actions.pack(fill="x", pady=(12, 0))
        tk.Button(actions, text="View Details", command=self.view_lead,
                  bg="white", fg=self.colors["text"], relief="flat", padx=14, pady=8).pack(side="left")
        tk.Button(actions, text="Edit Lead", command=self.edit_lead,
                  bg=self.colors["blue_soft"], fg=self.colors["blue"], relief="flat", padx=14, pady=8).pack(side="left", padx=8)
        if self.is_admin_or_manager():
            tk.Button(actions, text="Delete Lead", command=self.delete_lead,
                      bg="#FDECEC", fg=self.colors["red"], relief="flat", padx=14, pady=8).pack(side="left")
        self.lead_count_label = tk.Label(actions, text="", bg=self.colors["background"],
                                         fg=self.colors["muted"], font=("Segoe UI", 9))
        self.lead_count_label.pack(side="right")
        self.populate_leads_table(self.current_records)

    def populate_leads_table(self, records):
        if not hasattr(self, "lead_tree") or not self.lead_tree.winfo_exists():
            return
        for item in self.lead_tree.get_children():
            self.lead_tree.delete(item)
        self.displayed_records = list(records)
        for record in records:
            expected = record.get("expectedValue", 0)
            try:
                expected_text = f"{float(expected):,.2f}"
            except (TypeError, ValueError):
                expected_text = str(expected)
            created = str(record.get("createdDate", ""))
            if "T" in created:
                created = created.replace("T", " ")[:16]
            values = (
                record.get("leadCode", ""), record.get("leadName", ""),
                record.get("companyName", ""), record.get("source", ""),
                record.get("status", ""), record.get("priority", ""),
                expected_text, record.get("assignedToName", "Unassigned"), created
            )
            self.lead_tree.insert("", "end", iid=str(record.get("leadId")), values=values)
        self.lead_count_label.config(text=f"Showing {len(records)} lead(s)")

    def filter_leads(self):
        query = self.lead_search_entry.get().strip().lower()
        status = self.lead_status_filter.get()
        priority = self.lead_priority_filter.get()
        filtered = []
        for record in self.current_records:
            if status != "All" and record.get("status") != status:
                continue
            if priority != "All" and record.get("priority") != priority:
                continue
            searchable = " ".join(str(record.get(k, "")) for k in
                ("leadCode", "leadName", "companyName", "email", "phone", "source", "status", "priority", "assignedToName"))
            if query and query not in searchable.lower():
                continue
            filtered.append(record)
        self.populate_leads_table(filtered)

    def get_selected_lead(self):
        if not hasattr(self, "lead_tree") or not self.lead_tree.winfo_exists():
            return None
        selected = self.lead_tree.selection()
        if not selected:
            messagebox.showinfo("Select Lead", "Please select a lead first.")
            return None
        lead_id = selected[0]
        for record in self.current_records:
            if str(record.get("leadId")) == str(lead_id):
                return record
        return None

    def view_lead(self):
        record = self.get_selected_lead()
        if not record:
            return
        dialog = tk.Toplevel(self.root)
        dialog.title(f"Lead Details - {record.get('leadCode', '')}")
        dialog.geometry("620x610")
        dialog.configure(bg="white")
        dialog.transient(self.root)
        dialog.grab_set()
        tk.Label(dialog, text=record.get("leadName", "Lead"), bg="white",
                 fg=self.colors["text"], font=("Segoe UI", 19, "bold")).pack(anchor="w", padx=24, pady=(22, 4))
        tk.Label(dialog, text=f"{record.get('leadCode', '')}  •  {record.get('status', '')}  •  {record.get('priority', '')}",
                 bg="white", fg=self.colors["muted"], font=("Segoe UI", 10)).pack(anchor="w", padx=24, pady=(0, 14))
        box = tk.Text(dialog, wrap="word", font=("Segoe UI", 10), relief="flat", bg="#F6F8FC", padx=15, pady=12)
        box.pack(fill="both", expand=True, padx=24, pady=(0, 20))
        labels = [("Lead Code", "leadCode"), ("Lead Name", "leadName"), ("Email", "email"),
                  ("Phone", "phone"), ("Company", "companyName"), ("Source", "source"),
                  ("Status", "status"), ("Priority", "priority"), ("Expected Value", "expectedValue"),
                  ("Assigned To", "assignedToName"), ("Created", "createdDate")]
        for label, key in labels:
            box.insert("end", f"{label}: {record.get(key, '') or '—'}\n")
        box.config(state="disabled")

    def _lead_form(self, record=None):
        editing = record is not None
        dialog = tk.Toplevel(self.root)
        dialog.title("Edit Lead" if editing else "Create Lead")
        dialog.geometry("590x720")
        dialog.minsize(540, 650)
        dialog.configure(bg="white")
        dialog.transient(self.root)
        dialog.grab_set()

        tk.Label(dialog, text="Edit Lead" if editing else "Create a New Lead",
                 bg="white", fg=self.colors["text"], font=("Segoe UI", 20, "bold")).pack(anchor="w", padx=25, pady=(22, 3))
        tk.Label(dialog, text="Enter lead information. Fields marked * are required.",
                 bg="white", fg=self.colors["muted"], font=("Segoe UI", 9)).pack(anchor="w", padx=25, pady=(0, 14))

        form = tk.Frame(dialog, bg="white")
        form.pack(fill="both", expand=True, padx=25)
        form.columnconfigure(1, weight=1)
        widgets = {}
        row = 0

        def add_entry(label, key, required=False, initial=""):
            nonlocal row
            tk.Label(form, text=label + (" *" if required else ""), bg="white",
                     fg=self.colors["text"], font=("Segoe UI", 9, "bold")).grid(row=row, column=0, sticky="w", pady=7, padx=(0, 12))
            entry = tk.Entry(form, font=("Segoe UI", 10), relief="solid", bd=1)
            entry.grid(row=row, column=1, sticky="ew", pady=6, ipady=5)
            if initial is not None:
                entry.insert(0, str(initial))
            widgets[key] = entry
            row += 1

        def add_combo(label, key, values, initial):
            nonlocal row
            tk.Label(form, text=label, bg="white", fg=self.colors["text"],
                     font=("Segoe UI", 9, "bold")).grid(row=row, column=0, sticky="w", pady=7, padx=(0, 12))
            combo = ttk.Combobox(form, values=values, state="readonly", font=("Segoe UI", 10))
            combo.grid(row=row, column=1, sticky="ew", pady=6, ipady=3)
            combo.set(initial if initial in values else values[0])
            widgets[key] = combo
            row += 1

        add_entry("Lead Code", "leadCode", True, record.get("leadCode", "") if editing else "")
        add_entry("Lead Name", "leadName", True, record.get("leadName", "") if editing else "")
        add_entry("Email", "email", False, record.get("email", "") if editing else "")
        add_entry("Phone", "phone", False, record.get("phone", "") if editing else "")
        add_entry("Company Name", "companyName", False, record.get("companyName", "") if editing else "")
        sources = ["Website", "Referral", "Social Media", "Advertisement", "Cold Call", "Other"]
        priorities = ["Low", "Medium", "High"]
        statuses = ["New", "Contacted", "Qualified", "Unqualified", "Converted", "Lost"]
        add_combo("Source", "source", sources, record.get("source", "Other") if editing else "Other")
        add_combo("Priority", "priority", priorities, record.get("priority", "Medium") if editing else "Medium")

        if editing:
            current = record.get("status", "New")
            transitions = {
                "New": ["New", "Contacted", "Unqualified", "Lost"],
                "Contacted": ["Contacted", "Qualified", "Unqualified", "Lost"],
                "Qualified": ["Qualified", "Converted", "Lost"],
                "Unqualified": ["Unqualified", "New", "Lost"],
                "Lost": ["Lost"], "Converted": ["Converted"]
            }
            add_combo("Status", "status", transitions.get(current, [current]), current)
        else:
            add_combo("Status", "status", ["New"], "New")

        add_entry("Expected Value", "expectedValue", True,
                  record.get("expectedValue", 0) if editing else "0")

        if self.is_admin_or_manager():
            add_entry("Assigned Sales Executive ID", "assignedTo", True,
                      record.get("assignedTo", "") if editing else "")
            tk.Label(form, text="Enter the Identity user ID of an active Sales Executive.",
                     bg="white", fg=self.colors["muted"], font=("Segoe UI", 8)).grid(
                         row=row, column=1, sticky="w", pady=(0, 5))
            row += 1
        else:
            tk.Label(form, text="Assignment: Automatically assigned to your account.",
                     bg="#EEF4FF", fg=self.colors["blue"], font=("Segoe UI", 9), padx=10, pady=9).grid(
                         row=row, column=0, columnspan=2, sticky="ew", pady=10)
            row += 1

        footer = tk.Frame(dialog, bg="white")
        footer.pack(fill="x", padx=25, pady=18)
        tk.Button(footer, text="Cancel", command=dialog.destroy, bg="#EEF2F7", fg=self.colors["text"],
                  relief="flat", padx=18, pady=9).pack(side="right", padx=(8, 0))

        def save():
            def val(key):
                return widgets[key].get().strip()
            lead_code, lead_name = val("leadCode"), val("leadName")
            if not lead_code or not lead_name:
                messagebox.showwarning("Required Fields", "Lead Code and Lead Name are required.", parent=dialog)
                return
            try:
                expected = float(val("expectedValue"))
                if expected < 0:
                    raise ValueError
            except ValueError:
                messagebox.showwarning("Invalid Expected Value", "Enter a valid non-negative number.", parent=dialog)
                return
            payload = {
                "leadCode": lead_code, "leadName": lead_name,
                "email": val("email") or None, "phone": val("phone") or None,
                "companyName": val("companyName"), "source": val("source"),
                "status": val("status"), "priority": val("priority"),
                "expectedValue": expected
            }
            if self.is_admin_or_manager():
                assigned = val("assignedTo")
                if not assigned:
                    messagebox.showwarning("Assignment Required", "Enter an active Sales Executive's Identity user ID.", parent=dialog)
                    return
                payload["assignedTo"] = assigned
            endpoint = f"/api/leads/{record.get('leadId')}" if editing else "/api/leads"
            method = "PUT" if editing else "POST"
            try:
                response = self.api_request(method, endpoint, json=payload)
                if response is None:
                    return
                if response.status_code in ((200,) if editing else (200, 201)):
                    messagebox.showinfo("Success", "Lead updated successfully." if editing else "Lead created successfully.", parent=dialog)
                    dialog.destroy()
                    self.fetch_data("Leads")
                else:
                    self.show_api_error(response)
            except requests.exceptions.RequestException as error:
                messagebox.showerror("Connection Error", str(error), parent=dialog)

        tk.Button(footer, text="Save Changes" if editing else "Create Lead", command=save,
                  bg=self.colors["blue"], fg="white", activebackground=self.colors["blue_hover"],
                  activeforeground="white", relief="flat", padx=20, pady=9,
                  font=("Segoe UI", 9, "bold")).pack(side="right")

    def create_lead(self):
        self._lead_form()

    def edit_lead(self):
        record = self.get_selected_lead()
        if record:
            self._lead_form(record)

    def delete_lead(self):
        if not self.is_admin_or_manager():
            messagebox.showwarning("Permission Denied", "Only Admin and Manager can delete leads.")
            return
        record = self.get_selected_lead()
        if not record:
            return
        if not messagebox.askyesno("Confirm Delete",
            f"Delete lead {record.get('leadCode')} - {record.get('leadName')}?\n\nThe API will prevent deletion if related opportunities, follow-ups or activities exist."):
            return
        try:
            response = self.api_request("DELETE", f"/api/leads/{record.get('leadId')}")
            if response is None:
                return
            if response.status_code in (200, 204):
                messagebox.showinfo("Deleted", "Lead deleted successfully.")
                self.fetch_data("Leads")
            else:
                self.show_api_error(response)
        except requests.exceptions.RequestException as error:
            messagebox.showerror("Connection Error", str(error))

    # =================================================
    # OTHER MODULES - GENERIC TABLE
    # =================================================

    def display_generic_table(self, module):
        """Modern data workspace with role-aware CRUD/action controls."""
        for widget in self.content.winfo_children():
            widget.destroy()

        self.current_module = module
        self.set_active_nav(module)
        is_audit = module == "Audit Logs"

        heading = tk.Frame(self.content, bg=self.colors["background"])
        heading.pack(fill="x", padx=22, pady=(18, 10))
        title_box = tk.Frame(heading, bg=self.colors["background"])
        title_box.pack(side="left")
        tk.Label(title_box, text=module, font=("Segoe UI", 22, "bold"),
                 bg=self.colors["background"], fg=self.colors["text"]).pack(anchor="w")
        tk.Label(title_box, text=self.module_subtitle(module),
                 font=("Segoe UI", 9), bg=self.colors["background"],
                 fg=self.colors["muted"]).pack(anchor="w", pady=(3, 0))

        actions = tk.Frame(heading, bg=self.colors["background"])
        actions.pack(side="right")
        self.module_action_frame = actions
        if not is_audit:
            self.action_button(actions, "+ Add", lambda: self.open_module_form(module), self.colors["blue"])
            self.action_button(actions, "View", self.view_generic_record, "#E8EEF8", "#263247")
            self.action_button(actions, "Edit", self.edit_generic_record, "#FEF3C7", "#92400E")
            if self.is_admin_or_manager():
                self.action_button(actions, "Delete", self.delete_generic_record, "#FEE2E2", "#B91C1C")
        else:
            self.action_button(actions, "View details", self.view_generic_record, "#E8EEF8", "#263247")

        if module in ("Follow-ups", "Activities"):
            self.action_button(actions, "Complete", self.complete_generic_record, "#D1FAE5", "#065F46")
            if module == "Follow-ups":
                self.action_button(actions, "Mark missed", self.miss_followup, "#FFEDD5", "#9A3412")
            else:
                self.action_button(actions, "Cancel", self.cancel_activity, "#FEE2E2", "#B91C1C")

        self.action_button(actions, "Export CSV", self.export_current_records, "#E0E7FF", "#3730A3")
        self.action_button(actions, "Refresh", lambda: self.fetch_data(module), self.colors["blue"])

        search_frame = tk.Frame(self.content, bg=self.colors["surface"],
                                highlightbackground=self.colors["border"], highlightthickness=1)
        search_frame.pack(fill="x", padx=22, pady=(5, 10))
        tk.Label(search_frame, text="Search records", bg="white", fg=self.colors["muted"],
                 font=("Segoe UI", 9, "bold")).pack(side="left", padx=(13, 7), pady=11)
        self.generic_search = tk.Entry(search_frame, width=34, relief="flat", bg="#F4F7FC",
                                       fg=self.colors["text"], font=("Segoe UI", 10))
        self.generic_search.pack(side="left", padx=5, ipady=7, pady=5)
        self.generic_search.bind("<KeyRelease>", self.search_generic_records)
        self.generic_count = tk.Label(search_frame, text="", bg="white", fg=self.colors["muted"],
                                      font=("Segoe UI", 9))
        self.generic_count.pack(side="right", padx=14)

        table_frame = tk.Frame(self.content, bg="white", highlightbackground=self.colors["border"],
                               highlightthickness=1)
        table_frame.pack(fill="both", expand=True, padx=22, pady=(0, 20))

        preferred = {
            "Opportunities": ["opportunityId", "opportunityName", "customerName", "leadName", "amount", "probability", "weightedValue", "stage", "expectedCloseDate", "assignedToName"],
            "Follow-ups": ["followUpId", "subject", "followUpType", "followUpDate", "customerName", "leadName", "status", "assignedUserName"],
            "Activities": ["activityId", "activityType", "subject", "activityDate", "customerName", "leadName", "status", "assignedUserName"],
            "Audit Logs": ["auditLogId", "createdDate", "userName", "action", "entityName", "recordId", "result", "ipAddress"]
        }
        records = self.current_records
        columns = preferred.get(module, [])
        if records and columns:
            columns = [c for c in columns if any(c in r for r in records)]
        if not columns and records:
            columns = [k for k, v in records[0].items() if not isinstance(v, (dict, list))]
        if not columns:
            columns = ["Information"]
        self.generic_columns = columns
        self.generic_tree = ttk.Treeview(table_frame, columns=columns, show="headings", selectmode="browse")
        for column in columns:
            self.generic_tree.heading(column, text=column.replace("Id", " ID").replace("Name", " Name").replace("Date", " Date").title())
            self.generic_tree.column(column, width=145, minwidth=85, anchor="w")
        vs = ttk.Scrollbar(table_frame, orient="vertical", command=self.generic_tree.yview)
        hs = ttk.Scrollbar(table_frame, orient="horizontal", command=self.generic_tree.xview)
        self.generic_tree.configure(yscrollcommand=vs.set, xscrollcommand=hs.set)
        vs.pack(side="right", fill="y")
        hs.pack(side="bottom", fill="x")
        self.generic_tree.pack(side="left", fill="both", expand=True, padx=5, pady=5)
        self.generic_tree.bind("<Double-1>", self.view_generic_record)
        self.populate_generic_table(self.current_records)

    def module_subtitle(self, module):
        return {
            "Opportunities": "Manage deals, stages, expected revenue and weighted pipeline.",
            "Follow-ups": "Schedule customer and lead follow-ups and track outcomes.",
            "Activities": "Manage calls, meetings, emails and tasks.",
            "Audit Logs": "Read-only security and activity history. Admin access only."
        }.get(module, "Manage CRM records")

    def action_button(self, parent, text, command, bg, fg="white"):
        tk.Button(parent, text=text, command=command, bg=bg, fg=fg,
                  activebackground=bg, activeforeground=fg, relief="flat",
                  cursor="hand2", font=("Segoe UI", 8, "bold"), padx=10, pady=8).pack(side="left", padx=3)

    def selected_generic_record(self):
        selected = self.generic_tree.selection()
        if not selected:
            messagebox.showwarning("Select a record", "Please select a record first.")
            return None
        index = self.generic_tree.index(selected[0])
        if index >= len(self.displayed_records):
            return None
        return self.displayed_records[index]

    def open_module_form(self, module, record=None):
        """Reusable API-backed create/edit form for Opportunities, Follow-ups and Activities."""
        is_edit = record is not None
        win = tk.Toplevel(self.root)
        win.title(("Edit " if is_edit else "Add ") + module.rstrip("s"))
        win.geometry("650x760")
        win.minsize(560, 600)
        win.transient(self.root)
        win.grab_set()
        win.configure(bg="#F4F7FC")

        canvas = tk.Canvas(win, bg="#F4F7FC", highlightthickness=0)
        scroll = ttk.Scrollbar(win, orient="vertical", command=canvas.yview)
        form = tk.Frame(canvas, bg="white", padx=24, pady=20)
        form.bind("<Configure>", lambda e: canvas.configure(scrollregion=canvas.bbox("all")))
        canvas.create_window((0, 0), window=form, anchor="nw")
        canvas.configure(yscrollcommand=scroll.set)
        canvas.pack(side="left", fill="both", expand=True)
        scroll.pack(side="right", fill="y")

        tk.Label(form, text=("Edit " if is_edit else "Create ") + module.rstrip("s"),
                 bg="white", fg="#172033", font=("Segoe UI", 19, "bold")).pack(anchor="w")
        tk.Label(form, text="Fields marked with * are required. The server validates all submissions.",
                 bg="white", fg="#718096", font=("Segoe UI", 9)).pack(anchor="w", pady=(3, 18))

        widgets = {}
        values = record or {}
        def add_field(key, label, default="", kind="entry", options=None, required=False, height=3):
            tk.Label(form, text=label + (" *" if required else ""), bg="white", fg="#34415A",
                     font=("Segoe UI", 9, "bold")).pack(anchor="w", pady=(8, 5))
            value = values.get(key, default)
            if value is None: value = ""
            if kind == "combo":
                w = ttk.Combobox(form, values=options or [], state="readonly", font=("Segoe UI", 10))
                w.set(str(value) if str(value) else str(default))
                w.pack(fill="x", ipady=5)
            elif kind == "text":
                w = tk.Text(form, height=height, wrap="word", font=("Segoe UI", 10),
                            relief="flat", bg="#F4F7FC", fg="#172033", padx=8, pady=7)
                w.pack(fill="x")
                w.insert("1.0", str(value))
            else:
                w = tk.Entry(form, font=("Segoe UI", 10), relief="flat", bg="#F4F7FC",
                             fg="#172033", insertbackground="#172033")
                w.pack(fill="x", ipady=8)
                w.insert(0, str(value))
            widgets[key] = (w, kind)

        if module == "Opportunities":
            add_field("opportunityName", "Opportunity name", required=True)
            add_field("customerId", "Customer ID (optional; enter a valid ID)")
            add_field("leadId", "Lead ID (optional; enter a valid ID)")
            add_field("amount", "Amount", "0.01", required=True)
            add_field("probability", "Probability (%)", "50", required=True)
            add_field("stage", "Stage", "Qualification", "combo", ["Qualification", "Proposal", "Negotiation", "Won", "Lost"], True)
            add_field("expectedCloseDate", "Expected close date (YYYY-MM-DD)", (record or {}).get("expectedCloseDate", "")[:10] or __import__("datetime").date.today().isoformat(), required=True)
            add_field("source", "Source", "Other", "combo", ["Website", "Referral", "Advertisement", "Cold Call", "Existing Customer", "Other"], True)
            add_field("notes", "Notes", kind="text", height=4)
            if self.is_admin_or_manager(): add_field("assignedTo", "Assigned Sales Executive user ID", required=True)
        elif module == "Follow-ups":
            add_field("customerId", "Customer ID (optional)")
            add_field("leadId", "Lead ID (optional)")
            add_field("followUpDate", "Follow-up date (YYYY-MM-DD or YYYY-MM-DDTHH:MM:SS)", (record or {}).get("followUpDate", "")[:10] or __import__("datetime").date.today().isoformat(), required=True)
            add_field("subject", "Subject", required=True)
            add_field("followUpType", "Type", "Call", "combo", ["Call", "Email", "Meeting", "Other"], True)
            add_field("status", "Status", "Planned" if not is_edit else "Planned", "combo", ["Planned", "Completed", "Missed", "Cancelled"], True)
            add_field("notes", "Notes", kind="text", height=4)
            if self.is_admin_or_manager(): add_field("assignedUserId", "Assigned Sales Executive user ID", required=True)
        elif module == "Activities":
            add_field("activityType", "Activity type", "Task", "combo", ["Call", "Meeting", "Email", "Task"], True)
            add_field("subject", "Subject", required=True)
            add_field("description", "Description", kind="text", height=4)
            add_field("activityDate", "Activity date (YYYY-MM-DD or ISO datetime)", (record or {}).get("activityDate", "")[:10] or __import__("datetime").date.today().isoformat(), required=True)
            add_field("customerId", "Customer ID (optional)")
            add_field("leadId", "Lead ID (optional)")
            if is_edit:
                add_field("status", "Status", "Planned", "combo", ["Planned", "Completed", "Cancelled"], True)
            if self.is_admin_or_manager(): add_field("assignedTo", "Assigned Sales Executive user ID", required=True)

        def get_value(key):
            w, kind = widgets[key]
            return w.get("1.0", "end-1c").strip() if kind == "text" else w.get().strip()

        def optional_int(key):
            raw = get_value(key)
            if not raw: return None
            try: return int(raw)
            except ValueError: raise ValueError(f"{key} must be a whole number or left blank.")

        def submit():
            try:
                if module == "Opportunities":
                    payload = {
                        "opportunityName": get_value("opportunityName"),
                        "customerId": optional_int("customerId"),
                        "leadId": optional_int("leadId"),
                        "amount": float(get_value("amount")),
                        "probability": int(get_value("probability")),
                        "stage": get_value("stage"),
                        "expectedCloseDate": get_value("expectedCloseDate"),
                        "source": get_value("source"),
                        "notes": get_value("notes") or None
                    }
                    if self.is_admin_or_manager(): payload["assignedTo"] = get_value("assignedTo")
                    if not payload["customerId"] and not payload["leadId"]: raise ValueError("Link at least one Customer ID or Lead ID.")
                    if payload["amount"] <= 0 or not 0 <= payload["probability"] <= 100: raise ValueError("Amount must be greater than zero and probability must be 0–100.")
                elif module == "Follow-ups":
                    date_value = get_value("followUpDate")
                    if len(date_value) == 10: date_value += "T09:00:00"
                    payload = {"customerId": optional_int("customerId"), "leadId": optional_int("leadId"),
                               "followUpDate": date_value, "subject": get_value("subject"),
                               "followUpType": get_value("followUpType"),
                               "status": get_value("status") if is_edit else "Planned",
                               "notes": get_value("notes") or None}
                    if self.is_admin_or_manager(): payload["assignedUserId"] = get_value("assignedUserId")
                    if not payload["customerId"] and not payload["leadId"]: raise ValueError("Link at least one Customer ID or Lead ID.")
                else:
                    date_value = get_value("activityDate")
                    if len(date_value) == 10: date_value += "T09:00:00"
                    payload = {"activityType": get_value("activityType"), "subject": get_value("subject"),
                               "description": get_value("description") or None, "activityDate": date_value,
                               "customerId": optional_int("customerId"), "leadId": optional_int("leadId")}
                    if is_edit: payload["status"] = get_value("status")
                    if self.is_admin_or_manager(): payload["assignedTo"] = get_value("assignedTo")
                    if not payload["customerId"] and not payload["leadId"]: raise ValueError("Link at least one Customer ID or Lead ID.")

                endpoint = self.endpoints[module]
                method = "PUT" if is_edit else "POST"
                if is_edit:
                    record_id = record.get({"Opportunities":"opportunityId", "Follow-ups":"followUpId", "Activities":"activityId"}[module])
                    endpoint += f"/{record_id}"
                response = self.api_request(method, endpoint, json=payload)
                if response is None: return
                if response.status_code in (200, 201):
                    messagebox.showinfo("Success", module.rstrip("s") + (" updated." if is_edit else " created."))
                    win.destroy()
                    self.fetch_data(module)
                else:
                    self.show_api_error(response)
            except (ValueError, TypeError) as error:
                messagebox.showwarning("Check the form", str(error))
            except requests.exceptions.RequestException as error:
                messagebox.showerror("Connection error", str(error))

        footer = tk.Frame(form, bg="white")
        footer.pack(fill="x", pady=(22, 4))
        tk.Button(footer, text="Save record", command=submit, bg="#2563EB", fg="white",
                  activebackground="#1D4ED8", relief="flat", padx=20, pady=10,
                  font=("Segoe UI", 9, "bold")).pack(side="right")
        tk.Button(footer, text="Cancel", command=win.destroy, bg="#E8EEF8", fg="#263247",
                  relief="flat", padx=18, pady=10).pack(side="right", padx=8)

    def edit_generic_record(self):
        record = self.selected_generic_record()
        if not record: return
        module = self.current_module
        if module not in ("Opportunities", "Follow-ups", "Activities"):
            return
        self.open_module_form(module, record)

    def delete_generic_record(self):
        record = self.selected_generic_record()
        if not record: return
        if not self.is_admin_or_manager():
            messagebox.showwarning("Permission denied", "Only Admin and Manager can delete this record.")
            return
        module = self.current_module
        id_key = {"Opportunities":"opportunityId", "Follow-ups":"followUpId", "Activities":"activityId"}.get(module)
        if not id_key: return
        record_id = record.get(id_key)
        if not messagebox.askyesno("Confirm deletion", f"Delete selected {module.rstrip('s')} #{record_id}? This cannot be undone."):
            return
        response = self.api_request("DELETE", f"{self.endpoints[module]}/{record_id}")
        if response is None: return
        if response.ok:
            messagebox.showinfo("Deleted", f"{module.rstrip('s')} deleted successfully.")
            self.fetch_data(module)
        else: self.show_api_error(response)

    def run_record_action(self, suffix, success_text):
        record = self.selected_generic_record()
        if not record: return
        module = self.current_module
        id_key = {"Follow-ups":"followUpId", "Activities":"activityId"}.get(module)
        if not id_key: return
        record_id = record.get(id_key)
        response = self.api_request("POST", f"{self.endpoints[module]}/{record_id}/{suffix}")
        if response is None: return
        if response.ok:
            messagebox.showinfo("Updated", success_text)
            self.fetch_data(module)
        else: self.show_api_error(response)

    def complete_generic_record(self):
        if self.current_module in ("Follow-ups", "Activities"):
            self.run_record_action("complete", "Record marked as completed.")

    def miss_followup(self):
        if self.current_module == "Follow-ups":
            self.run_record_action("miss", "Follow-up marked as missed.")

    def cancel_activity(self):
        if self.current_module == "Activities":
            self.run_record_action("cancel", "Activity cancelled.")

    def export_current_records(self):
        import csv
        from tkinter import filedialog
        records = self.current_records or []
        if not records:
            messagebox.showinfo("Nothing to export", "There are no records to export.")
            return
        path = filedialog.asksaveasfilename(defaultextension=".csv", filetypes=[("CSV files", "*.csv")],
                                            initialfile=self.current_module.lower().replace(" ", "_") + ".csv")
        if not path: return
        keys = list(records[0].keys())
        try:
            with open(path, "w", newline="", encoding="utf-8-sig") as file:
                writer = csv.DictWriter(file, fieldnames=keys, extrasaction="ignore")
                writer.writeheader()
                for row in records:
                    writer.writerow({k: json.dumps(v, ensure_ascii=False) if isinstance(v, (dict, list)) else v for k, v in row.items()})
            messagebox.showinfo("Export complete", f"Exported {len(records)} records to:\n{path}")
        except OSError as error:
            messagebox.showerror("Export failed", str(error))

    def populate_generic_table(self, records):

        for item in self.generic_tree.get_children():
            self.generic_tree.delete(item)

        self.displayed_records = records

        columns = self.generic_tree["columns"]

        for record in records:

            values = []

            for column in columns:

                value = record.get(column, "")

                if isinstance(value, (dict, list)):
                    value = json.dumps(value)

                values.append(
                    str(value) if value is not None else ""
                )

            self.generic_tree.insert(
                "",
                "end",
                values=values
            )

        self.generic_count.config(
            text=f"{len(records)} record(s)"
        )

    def search_generic_records(self, event=None):

        query = (
            self.generic_search.get()
            .strip()
            .lower()
        )

        if not query:

            self.populate_generic_table(
                self.current_records
            )
            return

        filtered = []

        for record in self.current_records:

            if query in json.dumps(
                record,
                ensure_ascii=False
            ).lower():

                filtered.append(record)

        self.populate_generic_table(filtered)

    def view_generic_record(self, event=None):

        selected = self.generic_tree.selection()

        if not selected:
            return

        index = self.generic_tree.index(selected[0])

        if index >= len(self.displayed_records):
            return

        record = self.displayed_records[index]

        details = tk.Toplevel(self.root)

        details.title(f"{self.current_module} Details")
        details.geometry("650x550")

        text = tk.Text(
            details,
            wrap="word",
            font=("Consolas", 10)
        )

        text.pack(
            fill="both",
            expand=True,
            padx=15,
            pady=15
        )

        text.insert(
            "1.0",
            json.dumps(
                record,
                indent=4,
                ensure_ascii=False
            )
        )

        text.config(state="disabled")

    # =================================================
    # LOGOUT
    # =================================================

    def logout(self):

        self.access_token = None
        self.username = ""
        self.roles = []

        self.current_records = []
        self.displayed_records = []

        self.session.close()
        self.session = requests.Session()

        self.show_login()


# =====================================================
# APPLICATION ENTRY POINT
# =====================================================

if __name__ == "__main__":

    root = tk.Tk()

    app = RajibConnectClient(root)

    root.mainloop()
