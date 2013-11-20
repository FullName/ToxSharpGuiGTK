
using System;
using System.Drawing;
using System.Collections.Generic;
using Gtk;

using ToxSharpBasic;

namespace ToxSharpGTK
{
	public class HolderTreeNode : Gtk.TreeNode
	{
		public TypeIDTreeNode typeid;

		public HolderTreeNode(TypeIDTreeNode typeid)
		{
			this.typeid = typeid;
		}

		[Obsolete]
		public static HolderTreeNode Create(TypeIDTreeNode typeid)
		{
			return new HolderTreeNode(typeid);
		}

		public static HolderTreeNode HeaderNew(TypeIDTreeNode.EntryType type)
		{
			TypeIDTreeNode typeid = new HeaderTreeNode(type);
			return new HolderTreeNode(typeid);
		}
	}

	// GTK weirdness: must init/quit application outside, else it chokes
	public class ToxSharpGTK : Interfaces.IUIFactory
	{
		protected ToxSharpGTKMain wnd;

		public ToxSharpGTK()
		{
			Application.Init();
		}

		public Interfaces.IUIReactions Create()
		{
			if (wnd == null)
				wnd = new ToxSharpGTKMain();

			return wnd;
		}

		public void Quit()
		{
			wnd = null;
		}
	}

	public partial class ToxSharpGTKMain : Gtk.Window, Interfaces.IUIReactions
	{
		protected class Size
		{
			// SRLSY, PPL.
			public int width, height;
		}

		protected Size size;

		protected Interfaces.IUIActions uiactions;

		public ToxSharpGTKMain() : base (Gtk.WindowType.Toplevel)
		{
		}

		public void Init(Interfaces.IUIActions uiactions)
		{
			this.uiactions = uiactions;
			Application.Init();
			Build();

			ConnectState(false, null);
			TreesSetup();

			size = new Size();
			size.width = 0;
			size.height = 0;

			entry1.KeyReleaseEvent += OnEntryKeyReleased;
			DeleteEvent += OnDeleteEvent;

			Focus = entry1;
		}

		public void Run()
		{
			Show();
			Application.Run();
		}

	/*****************************************************************************/

		public void TitleUpdate(string name, string ID)
		{
			if ((name != null) && (ID != null))
				Title = "Tox# - " + name + " [" + ID + "]";
		}

		public void ConnectState(bool state, string text)
		{
			checkbutton1.Active = state;
			checkbutton1.Label = text;
		}

		public void ClipboardSend(string text)
		{
			Clipboard clipboard;

			// not X11: clipboard, X11: selection (but only pasteable with middle mouse button)
			clipboard = Clipboard.Get(Gdk.Selection.Clipboard);
			clipboard.Text = text;

			// X11: pasteable
			clipboard = Clipboard.Get(Gdk.Selection.Primary);
			clipboard.Text = text;
		}

	/*
	 *
	 *
	 *
	 */

		public void PopupMenuDo(object parent, Point position, Interfaces.PopupEntry[] entries)
		{
			if (entries.Length == 0)
				return;

			for(int i = 0; i < entries.Length; i++)
				if (entries[i].parent >= 0)
				{
				    // first parents, then children, no self-ref
					if (entries[i].parent >= i)
						return;

					// out of bounds
				    if (entries[i].parent >= entries.Length)
						return;
				}

			// generate lowest children
			// generate their parents
			// until all is tied in
			Gtk.Menu[] menus = new Gtk.Menu[entries.Length];
			Gtk.MenuItem[] items = new Gtk.MenuItem[entries.Length];

			for(int i = 0; i < entries.Length; i++)
			{
				Interfaces.PopupEntry entry = entries[i];

				items[i] = new Gtk.MenuItem(entry.title);
				items[i].Name = entry.action;
				if (entry.handle != null)
					items[i].Activated += entry.handle;
				items[i].Show();

				if (entry.parent >= 0)
				{
					if (menus[entry.parent] == null)
					{
						menus[entry.parent] = new Gtk.Menu();
						items[entry.parent].Sensitive = false;
						items[entry.parent].Submenu = menus[entry.parent];
					}

					menus[entry.parent].Append(items[i]);
				}
			}

			Gtk.Menu menu = new Gtk.Menu();

			for(int i = 0; i < entries.Length; i++)
				menu.Append(items[i]);

			// results in assert by GTK:
			// menu.Parent = parent as Gtk.Widget;
			menu.Popup();
		}

		public string PopupMenuAction(object o, System.EventArgs args)
		{
			Gtk.MenuItem item = o as Gtk.MenuItem;
			if (item != null)
				return item.Name;
			else
				return null;
		}

	/*
	 *
	 *
	 *
	 */

		public bool AskIDMessage(string message, string name1, string name2, out string input1, out string input2)
		{
			input1 = null;
			input2 = null;

			ToxSharpGTKInputTwoLines dlg = new ToxSharpGTKInputTwoLines();
			if (dlg.Do(message, name1, name2, out input1, out input2))
				return true;

			return false;
		}

	/*
	 *
	 *
	 *
	 */

		public void TextAdd(Interfaces.SourceType type, UInt32 id32, string source, string text)
		{
			if (id32 > UInt16.MaxValue)
				throw new ArgumentOutOfRangeException();

			UInt16 id = (UInt16)id32;
			long ticks = DateTime.Now.Ticks;
			liststoreall.AppendValues(source, text, (byte)type, id, ticks);

			if ((type == Interfaces.SourceType.Friend) || (type == Interfaces.SourceType.Group))
				foreach(ListStoreSourceTypeID liststore in liststorepartial)
					if ((liststore.type == type) && (liststore.id == id))
						liststore.AppendValues(source, text, (byte)type, id, ticks);
			
			Gtk.Adjustment adjust = nodescroll1.Vadjustment;
			adjust.Value = adjust.Upper - adjust.PageSize;
		}

	/*
	 *
	 *
	 *
	 */

		public bool CurrentTypeID(out Interfaces.SourceType type, out UInt32 id)
		{
			// send to target
			ScrolledWindow scrollwindow = notebook1.CurrentPageWidget as ScrolledWindow;
			NodeView nodeview = scrollwindow.Child as NodeView;
			ListStoreSourceTypeID liststore = nodeview.Model as ListStoreSourceTypeID;
			if (liststore == null)
			{
				type = 0;
				id = 0;
				return false;
			}

			type = liststore.type;
			id = liststore.id;
			return true;
		}

		protected void OnEntryKeyReleased(object o, Gtk.KeyReleaseEventArgs args)
		{
			Interfaces.InputKey key = Interfaces.InputKey.None;
			switch(args.Event.Key) {
				case Gdk.Key.Up: key = Interfaces.InputKey.Up; break;
				case Gdk.Key.Down: key = Interfaces.InputKey.Down; break;
				case Gdk.Key.Tab: key = Interfaces.InputKey.Tab; break;
				case Gdk.Key.Return: key = Interfaces.InputKey.Return; break;
				default: return;
			}

			if (uiactions.InputLine(entry1.Text, key))
				entry1.Text = "";
		}

	/*****************************************************************************/

		public void Quit()
		{
			uiactions.QuitPrepare();
			Application.Quit();
		}

	/*****************************************************************************/

		/* 
		 * other stuff
		 */

		[GLib.ConnectBefore]
		protected void OnDeleteEvent(object o, Gtk.DeleteEventArgs args)
		{
			Quit();
			args.RetVal = true;
		}
	}
}
