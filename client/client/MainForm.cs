﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;  //加入WMI
using System.IO;
using System.Net;

namespace client
{
	public partial class MainForm : Form
	{
		TcpClient Client;
		TcpListener Lis;
		Socket socket;
		NetworkStream stream;
		Socket Lis_socket;
		String localDiskList = "$GetDir||";                     //电脑盘符命令，初始化命令头
		String onlineOrder = "$Online||";                     //上线命令，初始化命令头部
		String folderList = "$GetFolder||";                  //列举子文件夹命令，初始化命令头
		String fileList = "$GetFile||";                    //列举文件命令，初始化命令头
		public delegate void myUI();

		public MainForm()
		{
			InitializeComponent();
		}
		/// <summary>
		/// 窗体加载时默认连接主控端主机
		/// 如果连接成功则发送上线请求
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Main_Form_Load(object sender, EventArgs e)
		{
			
		}
		#region  上线请求
		/// <summary>
		/// 此方法通过Windows WMI 服务,查询此客户端的信息
		/// </summary>
		public void Get_ComputerInfo()
		{
			//查询计算机名
			this.onlineOrder += this.WMI_Searcher("SELECT * FROM Win32_ComputerSystem", "Caption") + "||";
			//查询操作系统
			this.onlineOrder += this.WMI_Searcher("SELECT * FROM Win32_OperatingSystem", "Caption") + "||";
			//查询CPU
			this.onlineOrder += this.WMI_Searcher("SELECT * FROM Win32_Processor", "Caption") + "||";
			//查询内存容量 - 单位: MB
			this.onlineOrder += (int.Parse(this.WMI_Searcher("SELECT * FROM Win32_OperatingSystem", "TotalVisibleMemorySize")) / 1024) + " MB||";
		}


		/// <summary>
		/// 此方法根据指定语句通过WMI查询用户指定内容
		/// 并且返回
		/// </summary>
		/// <param name="QueryString"></param>
		/// <param name="Item_Name"></param>
		/// <returns></returns>
		public String WMI_Searcher(String QueryString, String Item_Name)
		{
			String Result = "";
			ManagementObjectSearcher MOS = new ManagementObjectSearcher(QueryString);
			ManagementObjectCollection MOC = MOS.Get();
			foreach (ManagementObject MOB in MOC)
			{
				Result = MOB[Item_Name].ToString();
				break;
			}
			MOC.Dispose();
			MOS.Dispose();
			return Result;
		}

		/// <summary>
		/// 此方法用于向主控端发送上线请求 
		/// 命令原型 : $Online||计算机名||操作系统||CPU频率||内存容量
		/// </summary>
		public void postOnlineMessage()
		{
			this.Client = new TcpClient();
			//多次尝试连接
			while (Global.isListenPort)
			{
				try
				{
					this.Client.Connect(Global.Host, Global.Port);
				}
				catch
				{ }
				if (this.Client.Connected)
					break;
			}
			//如果连接上了
			if (this.Client.Connected)
			{
				//弹框点击确定
				if (this.bombInfo() == 1)
				{
					//得到套接字原型
					this.socket = this.Client.Client;
					this.stream = new NetworkStream(this.socket);
					//发送上线请求
					this.stream.Write(Encoding.Default.GetBytes(this.onlineOrder), 0, Encoding.Default.GetBytes(this.onlineOrder).Length);
					this.stream.Flush();
					//如果请求发出后套接字仍然处于连接状态
					//则单劈出一个线程，用于接收命令
					if (this.socket.Connected)
					{
						Thread thread = new Thread(new ThreadStart(this.Get_Server_Order));
						thread.Start();
					}
				}
				
			}
		}
		public int bombInfo()
		{
			//跳出弹框
			//label1.Text = "";
			DialogResult MsgBoxResult;//设置对话框的返回值
			MsgBoxResult = MessageBox.Show("远程主机127.0.0.1想要对您进行远程管理", "提示",
			MessageBoxButtons.YesNo,
			MessageBoxIcon.Information,
			MessageBoxDefaultButton.Button2);
			if (MsgBoxResult == DialogResult.Yes)//如果对话框的返回值是YES（按"Y"按钮）
			{
				//this.label1.ForeColor = System.Drawing.Color.Red;
				//label1.Text = " 你选择了按下”Yes“的按钮！"
				return 1;

			}
			return 0;
		}
		#endregion

		#region  监听端口，处理命令
		/// <summary>
		/// 此方法用于监听接收信息的端口
		/// </summary>
		public void Listen_Port()
		{
			while (Global.isListenPort)
			{
				this.Lis_socket = Lis.AcceptSocket();  //如果有服务端请求则创建套接字
				Thread thread = new Thread(new ThreadStart(this.Res_Message));
				thread.Start();
			}
		}
		/// <summary>
		/// 此方法用于得到主控端发来的命令集合
		/// </summary>
		public void Get_Server_Order()
		{
			while (true)
			{
				try
				{
					byte[] bb = new byte[1024];
					//接收命令
					int Order_Len = this.stream.Read(bb, 0, bb.Length);
					//得到主控端发来的命令集合
					String[] Order_Set = Encoding.Default.GetString(bb, 0, Order_Len).Split(new String[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
					this.Order_Catcher(Order_Set);
				}
				catch (Exception ex)
				{ };
			}
		}

		/// <summary>
		/// 此方法负责接收主控端命令
		/// 并且传递到处理方法种
		/// </summary>
		public void Res_Message()
		{
			while (true)
			{
				try
				{
					using (NetworkStream ns = new NetworkStream(this.Lis_socket))
					{
						try
						{
							byte[] bb = new byte[1024];
							//得到命令
							int Res_Len = ns.Read(bb, 0, bb.Length);
							//得到完整命令分割后的数组结构
							String[] Order_Set = Encoding.Default.GetString(bb, 0, Res_Len).Split(new String[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
							//调用判断命令函数
							//MessageBox.Show(Order_Set[0]);
							this.Order_Catcher(Order_Set);
						}
						catch (Exception ex) { };
					}
				}
				catch (Exception ex)
				{ };
			}
		}

		/// <summary>
		/// 此方法用于判断命令结构
		/// 根据不同的命令调用不同的方法进行处理
		/// </summary>
		/// <param name="Order_Set"></param>
		public void Order_Catcher(String[] Order_Set)
		{
			switch (Order_Set[0])
			{
				//此命令头表示客户端状态结果返回
				case "$Return":
					switch (Order_Set[1])
					{
						//如果是上线成功
						case "#Online_OK":
							this.Online_OK();
							break;
					}
					break;
				//此命令头表示客户端请求本机所有盘符
				case "$GetDir":
					this.getLocalDisk();
					break;
				//此命令头表示客户端请求本机指定目录下的所有文件夹
				case "$GetFolder":
					this.getFoloder(Order_Set[1]);
					break;
				//此命令头表示客户端请求本机指定目录下的所有文件
				case "$GetFile":
					this.getFile(Order_Set[1]);
					break;
			}
		}
		#endregion

		/// <summary>
		/// 上线成功后的用户提示
		/// </summary>
		public void Online_OK()
		{
			//弹框
		}
		

		#region  文件管理
		/// <summary>
		/// 此方法调用Windows WMI
		/// 列举当前电脑所有盘符
		/// </summary>
		public void getLocalDisk()
		{
			this.localDiskList = "$GetDir||";
			ManagementObjectSearcher MOS = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk");
			ManagementObjectCollection MOC = MOS.Get();
			foreach (ManagementObject MOB in MOC)
			{
				this.localDiskList += MOB["Description"].ToString() + "#" + MOB["Caption"].ToString() + ",";
			}
			MOC.Dispose();
			MOS.Dispose();

			try
			{
				//得到硬盘分区列表后，尝试发送
				using (NetworkStream Ns = new NetworkStream(this.Lis_socket))
				{
					Ns.Write(Encoding.Default.GetBytes(this.localDiskList), 0, Encoding.Default.GetBytes(this.localDiskList).Length);
					Ns.Flush();
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("尝试发送硬盘分区列表失败 : " + ex.Message);
			}
		}

		/// <summary>
		/// 此方法用于根据指定盘符列举子文件夹
		/// </summary>
		/// <param name="Path"></param>
		public void getFoloder(String Path)
		{
			this.folderList = "$GetFolder||";
			//得到指定盘符的所有子文件夹
			String[] Folder = Directory.GetDirectories(Path);
			for (int i = 0; i < Folder.Length; i++)
			{
				this.folderList += Folder[i] + ",";
			}

			try
			{
				//得到文件夹列表后，尝试发送
				using (NetworkStream Ns = new NetworkStream(this.Lis_socket))
				{
					Ns.Write(Encoding.Default.GetBytes(this.folderList), 0, Encoding.Default.GetBytes(this.folderList).Length);
					Ns.Flush();
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("尝试发送文件夹列表失败 : " + ex.Message);
			}
		}

		/// <summary>
		/// 此方法用于根据指定盘符列举子所有文件
		/// </summary>
		/// <param name="Path"></param>
		public void getFile(String Path)
		{
			this.fileList = "$GetFile||";
			//得到文件目标文件夹文件数组
			String[] Result_List = Directory.GetFiles(Path);
			//通过拆分得到结果字符串
			for (int i = 0; i < Result_List.Length; i++)
			{
				this.fileList += Result_List[i] + ",";
			}

			try
			{
				//得到文件列表后，尝试发送
				using (NetworkStream Ns = new NetworkStream(this.Lis_socket))
				{
					Ns.Write(Encoding.Default.GetBytes(this.fileList), 0, Encoding.Default.GetBytes(this.fileList).Length);
					Ns.Flush();
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("尝试发送文件夹列表失败 : " + ex.Message);
			}
		}
		#endregion

		/// <summary>
		/// 窗体关闭
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Main_Form_FormClosing(object sender, FormClosingEventArgs e)
		{
			//下线命令 原型 ： $OffLine||
			String Order = "$OffLine||";
			try
			{
				//尝试发送下线请求
				this.stream.Write(Encoding.Default.GetBytes(Order + ((IPEndPoint)this.socket.LocalEndPoint).Address.ToString()), 0, Encoding.Default.GetBytes(Order + ((IPEndPoint)this.socket.LocalEndPoint).Address.ToString()).Length);
				this.stream.Flush();
			}
			catch (Exception ex)
			{ };
			Environment.Exit(0);
		}

		/// <summary>
		/// 连接主控端主机
		/// 如果连接成功则发送上线请求
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void button1_Click(object sender, EventArgs e)
		{
			//调用WMI收集系统信息
			this.Get_ComputerInfo();
			//发送上线请求 - [多线程]
			Thread thread = new Thread(new ThreadStart(this.postOnlineMessage));
			thread.Start();
			//自身监听端口,用于接收信息
			Lis = new TcpListener(Global.lisPort);
			Lis.Start();  //一直监听
			Thread thread_Lis_MySelf = new Thread(new ThreadStart(this.Listen_Port));
			thread_Lis_MySelf.Start();
		}
	}

}