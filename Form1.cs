using AutoHome2.Database;
using AutoHome2.Objetos;
using AutoHome2.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Media;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Speech.Recognition;
using System.Globalization;
using System.Windows.Forms;
using WindowsFormsApplication2.Src.Classes;
using NAudio.Wave;
using AutoHome2.Utils;
using System.Net;
using System.Net.Sockets;

namespace AutoHome2
{


    public partial class Form1 : Form, ChangeMusicListenner
    {
        private bool isOpen = false;
        public static bool isSound = false;
        private String[] ports;
        private string[] auxiliar;
        private List<Horarios> mHorarios;
        private List<Comandos> comandos = new List<Comandos>();
        private SpeechSynthesizer sintetizador;
        public SpeechRecognitionEngine sre;
        List<Dispositivos> mDispositivos;
        private int volumeAudio = 50;
        private playr playr;
        private Boolean telaCheia = false;

        //Sockets
        private Socket _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private byte[] _buffer = new byte[1024];
        private List<SocketT2h> clientSockets;

        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
            clientSockets = new List<SocketT2h>();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            SetupServer();//Sockets
            lblEndIp.Text = "Endereço IP: " + util.GetLocalIPAddress();
            try
            {
                sintetizador = new SpeechSynthesizer();
                //sintetizador.SelectVoice("IVONA 2 Ricardo");
            }
            catch
            {
                MessageBox.Show("Não foi possivel iniciar o sintetizador de voz.");
            }
            ports = SerialPort.GetPortNames();
            foreach (string rw in ports)
            {
                ToolStripMenuItem SSMenu = new ToolStripMenuItem(rw, Properties.Resources.imgConected, ChildClick);
                portaToolStripMenuItem.DropDownItems.Add(SSMenu);
            }
            if (ports.Length > 0)
            {
                portaToolStripMenuItem.Text = "Porta: " + ports[0];
            }
            //conectar();

            /*Thread.CurrentThread.Priority = ThreadPriority.Highest;
            sre = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("pt-BR"));
            sre.SetInputToDefaultAudioDevice();
            sre.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(Analyse);
            sre.AudioLevelUpdated += new EventHandler<AudioLevelUpdatedEventArgs>(Audio);
            loadingGrammar();
            sre.RecognizeAsync(RecognizeMode.Multiple);*/
        }

        //CONEXÃO RS232
        private void btnConectar_Click(object sender, EventArgs e)
        {
            if (!isOpen)
            {
                if (!serialPort1.IsOpen)
                {

                }
            }
            else
            {
                serialPort1.Close();
                statusConexao.Text = "Status: Desconectado";
                isOpen = false;

            }
        }

        public void conectar()
        {
            if (!serialPort1.IsOpen)
            {
                try
                {
                    serialPort1.PortName = "COM3";
                    serialPort1.BaudRate = 9600;
                    serialPort1.Parity = Parity.None;
                    serialPort1.StopBits = StopBits.One;
                    serialPort1.DataBits = 8;
                    serialPort1.Handshake = Handshake.None;
                    serialPort1.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                    serialPort1.Open();
                    isOpen = true;
                    conectarToolStripMenuItem1.Text = "Desconectar";
                    statusConexao.Text = "Status: Conectado";
                    statusConexao.Image = Properties.Resources.imgConected;
                    sintetizador.SpeakAsync("Sistema de Automação Residencial iniciado com sucesso!");
                }
                catch (Exception erro)
                {
                    sintetizador.SpeakAsync("Não foi possível se conectar com o conversor. Verifique se está conectado corretamente!");
                    MessageBox.Show("Erro ao conectar com o dispositivo. Verifique se está conectado corretamente!");

                }
            }

        }
        //CONEXÃO RS232

        private void conectarToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (isOpen)
            {
                serialPort1.Close();
                conectarToolStripMenuItem1.Text = "Conectar";
                statusConexao.Text = "Status: Desconectado";
                statusConexao.Image = Properties.Resources.imgDisconnected;
                isOpen = false;

            }
            else
            {
                conectar();
            }
        }



        //MÉTODOS SOCKET
        /// <SetupServer>
        /// Configura o Servidor Socket com a porta desejada
        /// </SetupServer>
        private void SetupServer()
        {
            _serverSocket.Bind(new IPEndPoint(IPAddress.Any, 8000));
            _serverSocket.Listen(1);
            _serverSocket.BeginAccept(new AsyncCallback(AppceptCallback), null);
        }

        /// <AppceptCallback>
        /// Quando o Servidor Socket aceita uma conexão com o Cliente
        /// Então o cliente é gravado na lista clientSockets e
        /// é iniciada uma Thread para ouvi-lo
        /// </AppceptCallback>
        /// <param name="ar"></param>
        private void AppceptCallback(IAsyncResult ar)
        {
            
            Socket socket = _serverSocket.EndAccept(ar);
            clientSockets.Add(new SocketT2h(socket));

            socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), socket);
            _serverSocket.BeginAccept(new AsyncCallback(AppceptCallback), null);

        }

        /// <ReceiveCallback>
        /// Quando o cliente envia um comando este método é executado
        /// </ReceiveCallback>
        /// <param name="ar"></param>
        private void ReceiveCallback(IAsyncResult ar)
        {
            //MessageBox.Show("CHEGOU COMANDO");
            Socket socket = (Socket)ar.AsyncState;
            if (socket.Connected)
            {
                int received;
                try
                {
                    received = socket.EndReceive(ar);
                }
                catch (Exception)
                {
                    
                    //Tentou se comunicar com cliente mas o mesmo não recebeu -> remove cliente
                    for (int i = 0; i < clientSockets.Count; i++)
                    {
                        if (clientSockets[i]._Socket.RemoteEndPoint.ToString().Equals(socket.RemoteEndPoint.ToString()))
                        {
                            listBoxClientes.Items.RemoveAt(i);
                            clientSockets.RemoveAt(i);
                            
                        }
                    }

                    return;
                }
                if (received != 0)
                {
                    byte[] dataBuf = new byte[received];
                    Array.Copy(_buffer, dataBuf, received);
                    string text = Encoding.UTF8.GetString(dataBuf);
                    string reponse = string.Empty;
                    if (text.StartsWith("@@"))
                    {
                        String mac;
                        mac = text.Replace("@@", "");
                        mac = mac.Trim();
                        listBoxClientes.Items.Add(mac.ToUpper());
                        clientSockets[(clientSockets.Count - 1)]._Name = mac;
                        for (int i = 0; i < clientSockets.Count; i++)
                        {
                            if (i != (clientSockets.Count - 1))
                            {
                                if (clientSockets[i]._Name != null)
                                {
                                    clientSockets.RemoveAt(i);
                                    listBoxClientes.Items.RemoveAt(i);

                                }

                            }
                        }
                    }
                    else
                    {

                        command(text, socket);
                    }
                }
                else
                {
                    //Quando o Cliente se desconecta
                    try
                    {
                        for (int i = 0; i < clientSockets.Count; i++)
                        {

                            if (clientSockets[i]._Socket.RemoteEndPoint.ToString().Equals(socket.RemoteEndPoint.ToString()))
                            {
                            
                                clientSockets.RemoveAt(i);
                                listBoxClientes.Items.RemoveAt(i);
                                return;

                            }
                        }
                    }
                    catch { return; }
                }
            }
            try
            {
                Debug.WriteLine("sq");
                socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), socket);
            }
            catch { }
        }

        /// <Sendata>
        /// Quando o Servidor manda um comando para o cliente
        /// </Sendata>
        /// <param name="socket"></param>
        /// <param name="noidung"></param>
        void Sendata(Socket socket, string noidung)
        {
            Debug.WriteLine(noidung);
            byte[] data = Encoding.UTF8.GetBytes(noidung);

            socket.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendCallback), socket);
            _serverSocket.BeginAccept(new AsyncCallback(AppceptCallback), null);
        }

        /// <SendCallback>
        /// 
        /// </SendCallback>
        /// <param name="AR"></param>
        private void SendCallback(IAsyncResult AR)
        {
            Socket socket = (Socket)AR.AsyncState;
            socket.EndSend(AR);
        }

        /// <command>
        /// Envio de comando para o cliente
        /// Caso argumento socket seja nulo é enviado para todos os clientes
        /// </command>
        /// <param name="read"></param>
        /// <param name="socket"></param>
        private void command(String read, Socket socket)
        {

            try
            {

                auxiliar = read.Split('|');

                if (auxiliar.Length > 0)
                {
                    if (auxiliar[0].Equals("PREVISAO"))
                    {
                        StreamWriter valor = new StreamWriter(@"C:\p\notification.txt");
                        sintetizador.SpeakAsync("Um momento, irei verificar!");
                        valor.Close();
                        Thread.Sleep(3000);
                        Debug.WriteLine("PREVISAO");
                        WebService.GetWebService(previsao);
                    }
                    else
                    {
                        if (auxiliar[0].Equals("CONSUMO"))
                        {
                            String sDate = DateTime.Now.ToString();
                            sDate = sDate.Substring(0, 10);
                            String sql = "SELECT descricao FROM Dispositivos WHERE nome = '" + auxiliar[2] + "' AND endereco = " + auxiliar[1];
                            String dispositivo = database.selectSQLAcesso(sql);
                            sql = "SELECT Acessos.comando,Acessos.horario,Dispositivos.descricao,Dispositivos.endereco,Usuarios.nome, Dispositivos.codigoDispositivo,Dispositivos.codigoDispositivo FROM (Acessos INNER JOIN Dispositivos ON Dispositivos.codigoDispositivo = Acessos.codigoDispositivo) INNER JOIN Usuarios ON Acessos.codigoUsuario = Usuarios.codigoUsuario WHERE Acessos.horario like '" + sDate + "%' AND Dispositivos.descricao ='" + dispositivo + "'";
                            List<Acessos> listaAcessos = database.selectSQLAcessos(sql);
                            List<Double> mList = Utils.util.consumo(listaAcessos);
                            StreamWriter valor = new StreamWriter(@"C:\p\notification.txt");

                            valor.Write("Verifiquei que o dispositivo: " + dispositivo + " teve um consumo hoje de " + mList[1] + " quilo uóti hora. E teve até o momento de hoje " + mList[0] + " horas de funcionamento com um gasto de " + Math.Round((mList[1] * 0.57069801), 2) + " reais");
                            valor.Close();

                        }
                        else
                        {
                            if (auxiliar[0].Equals("PLAYER"))
                            {
                                foreach (SocketT2h st in clientSockets)
                                {
                                    if (st._Socket == socket)
                                    {
                                        st._Screen = "PLAYER";
                                        break;
                                    }
                                }

                                if (auxiliar[1].Trim().Equals("ARTISTAS"))
                                {
                                    String fileName = "ARTISTAS|";
                                    if (System.IO.Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)))
                                    {
                                        string[] files = System.IO.Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));

                                        foreach (string m in files)
                                        {
                                            fileName = fileName + System.IO.Path.GetFileName(m) + "|";

                                        }

                                        Sendata(socket, fileName);
                                    }
                                    else
                                    {
                                        Sendata(socket, "@Pasta Minhas Músicas não encontrada");
                                    }
                                }
                                else
                                {
                                    if (auxiliar[1].Trim().Equals("MUSICAS"))
                                    {

                                        String fileName = "MUSICAS|";
                                        int index = -1;
                                        if (playr != null)
                                        {
                                            if (playr.isPlaying())
                                            {
                                                if (auxiliar[2].Trim().Equals(playr.getArtista()))
                                                {
                                                    index = playr.getMusicIndex();
                                                }
                                            }
                                        }
                                        fileName = fileName + index + "|";
                                        if (System.IO.Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) + "\\" + auxiliar[2].Trim()))
                                        {
                                            string[] files = System.IO.Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) + "/" + auxiliar[2].Trim());

                                            foreach (string m in files)
                                            {

                                                if (System.IO.Path.GetExtension(m).Equals(".mp3"))
                                                {
                                                    fileName = fileName + System.IO.Path.GetFileNameWithoutExtension(m) + "|";
                                                }

                                            }

                                            Sendata(socket, fileName);
                                        }
                                        else
                                        {
                                            Sendata(socket, "@Pasta Minhas Músicas não encontrada");
                                        }
                                    }
                                    else
                                    {
                                        if (auxiliar[1].Trim().Equals("PLAY"))
                                        {
                                            var playlist = new List<string>();
                                            string[] files = System.IO.Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) + "/" + auxiliar[2].Trim());
                                            foreach (string m in files)
                                            {
                                                if (System.IO.Path.GetExtension(m).Equals(".mp3"))
                                                    playlist.Add(m);
                                            }
                                            if (playr != null)
                                            {
                                                if (playr.isPlaying())
                                                {
                                                    playr.StopSong();
                                                }
                                            }
                                            playr = new playr(playlist, int.Parse(auxiliar[3].Trim()), volumeAudio / 100f, this);
                                            playr.PlaySong();
                                        }
                                        else
                                        {
                                            if (auxiliar[1].Trim().Equals("VOLUME"))
                                            {

                                                try
                                                {
                                                    volumeAudio = int.Parse(auxiliar[2].ToString());

                                                    trackBarAudioPlayer.Value = volumeAudio;

                                                    if (playr != null)
                                                    {
                                                        float vol = float.Parse(auxiliar[2].ToString());
                                                        playr.SetVolume(vol / 100f);
                                                    }
                                                }
                                                catch { }

                                            }
                                            else
                                            {
                                                if (auxiliar[1].Trim().Equals("CONTROL"))
                                                {
                                                    if (auxiliar[2].Trim().Equals("PLAY")) {
                                                        playr.ResumeSong(); 

                                                    }
                                                    else if (auxiliar[2].Trim().Equals("STOP")) playr.StopSong();
                                                    else if (auxiliar[2].Trim().Equals("NEXT")) playr.NextSong();
                                                    else if (auxiliar[2].Trim().Equals("BACK")) playr.BackSong();
                                                    else if (auxiliar[2].Trim().Equals("PAUSE")) playr.PauseSong();
                                                }
                                                else
                                                {
                                                    if (auxiliar[1].Trim().Equals("CHECK"))
                                                    {
                                                        if (auxiliar[2].Trim().Equals("VOLUME"))
                                                        {

                                                            Sendata(socket, "PLAYER|STATUS|VOLUME|" + volumeAudio);

                                                        }
                                                        else
                                                        {
                                                            
                                                            String send = "PLAYER|STATUS|PLAYING|NOTHING";
                                                            if (auxiliar[2].Trim().Equals("PLAYING"))
                                                            {
                                                                if (playr != null)
                                                                {
                                                                    if (playr.isPlaying())
                                                                    {
                                                                        send=playr.howPlaying();
                                                                        
                                                                    }
                                                                }
                                                            }
                                                           send=send + "|VOLUME|" + volumeAudio;
                                                           Sendata(socket, send);
;                                                      }
                                                    }
                                                }
                                            }

                                        }
                                    }
                                }


                            }
                            else
                            {

                                int codUsuario = util.checkMac(auxiliar[4]);//Checa se o MAC é permitido
                                int codDispositivo = util.checkDispositivos(auxiliar[0], Int32.Parse(auxiliar[1]));//Checa se o Dispotivo está cadastrado
                                if (codUsuario != -1 && codDispositivo != -1)
                                {

                                    if (auxiliar[2].Equals("W"))
                                    {
                                        string send = "1|" + auxiliar[1] + "|" + auxiliar[0] + "|" + auxiliar[2] + "|" + auxiliar[3];
                                        serialPort1.Write(send);
                                        Debug.WriteLine(send);
                                        statusReceiver.Text = "Último Comando Enviado: " + auxiliar[0] + "|" + auxiliar[2] + "|" + auxiliar[3] + "|" + "SILLAS";
                                        if (auxiliar[5].Equals("s1"))
                                        {

                                        }
                                        String comando = database.selectSQLAcesso("SELECT LAST(comando) FROM Acessos WHERE codigoDispositivo=" + codDispositivo);
                                        if (!comando.Equals(auxiliar[3]))
                                        {
                                            String sql = "INSERT INTO Acessos (codigoUsuario,codigoDispositivo,comando) VALUES ("
                                                        + codUsuario + ","
                                                        + codDispositivo + ","
                                                        + "'" + auxiliar[3] + "')";
                                            Database.database.insertSQL(sql);
                                        }
                                    }
                                    else
                                    {
                                        string send = "1|" + auxiliar[1] + "|" + auxiliar[0] + "|" + auxiliar[2] + "|" + auxiliar[3];
                                        serialPort1.Write(send);
                                        statusReceiver.Text = "Último Comando Enviado: " + auxiliar[0] + "|" + auxiliar[1] + "|" + "SILLAS";

                                    }
                                }
                                if (auxiliar[5].Equals("s1"))
                                {

                                }
                            }
                        }
                    }
                }

            }
            catch (Exception k)
            {
                Console.Write("ERRO AO PROCESSAR COMANDO: "+k);
            }
        }
        //MÉTODOS SOCKET


        //MÉTODOS COMANDO DE VOZ
        /// <loadingGrammar>
        /// Gramáticas com os comandos de Voz
        /// </loadingGrammar>
        private void loadingGrammar()
        {
            string[] lines = File.ReadAllLines(Environment.CurrentDirectory + "\\example.txt");
            Choices cPalavras = new Choices();
            cPalavras.Add(lines);
            List<Grammar> grammars = new List<Grammar>();
            Grammar wordsList = new Grammar(new GrammarBuilder(cPalavras));
            wordsList.Name = "Comandos";
            sre.LoadGrammar(wordsList);

            Choices texts = new Choices();
            lines = File.ReadAllLines(Environment.CurrentDirectory + "\\conversas.txt");

            foreach (string line in lines)
            {
                if (line.StartsWith("--") || line == String.Empty) continue;
                var parts = line.Split(new char[] { '|' });
                comandos.Add(new Comandos() { pergunta = parts[0], resposta = parts[1] });
                texts.Add(parts[0]);

            }
            wordsList = new Grammar(new GrammarBuilder(texts));
            wordsList.Name = "Conversas";
            sre.LoadGrammar(wordsList);



            GrammarBuilder gb = new GrammarBuilder();
            String sql = "SELECT *FROM Dispositivos";
            mDispositivos = database.selectSQLDispositivos(sql);
            Choices cDispositivos = new Choices();
            if (mDispositivos.Count != 0)
            {
                gb.Append(new Choices(new string[] { "Max" }));
                gb.Append(new Choices(new string[] { "desligar", "ligar", "acender", "apagar" }));
                foreach (Dispositivos d in mDispositivos)
                {
                    cDispositivos.Add(d.nome);
                }
                gb.Append(cDispositivos);

            }
            Grammar gDispositivos = new Grammar(gb);
            gDispositivos.Name = "Dispositivos";
            sre.LoadGrammar(gDispositivos);

            GrammarBuilder musicas = new GrammarBuilder();
            Choices cMusicas = new Choices();
            musicas.Append(new Choices(new string[] { "Max tocar" }));
            if (System.IO.Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)))
            {
                string[] files = System.IO.Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
                foreach (string s in files)
                {
                    String fileName = System.IO.Path.GetFileName(s);
                    cMusicas.Add(fileName);
                    // MessageBox.Show(fileName);
                }
                musicas.Append(cMusicas);
            }
            ;
            Grammar gMusicas = new Grammar(musicas);
            gMusicas.Name = "Musicas";
            sre.LoadGrammar(gMusicas);




        }

        /// <Analyse>
        /// Resultado do processamento do comando de voz
        /// </Analyse>
        /// <param name="s"></param>
        /// <param name="e"></param>
        private void Analyse(object s, SpeechRecognizedEventArgs e)
        {
            string grammar = e.Result.Grammar.Name;
            string command = e.Result.Text;
            command = command.ToLower();
            if (grammar.Equals("Comandos"))
            {
                comandosAnalyse(command);

            }
            else
            {
                if (grammar.Equals("Musicas"))
                {
                    if (playr != null)
                    {
                        playr.StopSong();
                    }
                    string task = "";
                    task = command.Replace("max tocar", "");
                    task = task.Trim();
                    sintetizador.Speak("Ok. Tocando " + task);
                    var playlist = new List<string>(); ;
                    string[] files = System.IO.Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) + "/" + task);
                    foreach (string m in files)
                    {
                        playlist.Add(m);
                    }

                    playr = new playr(playlist, volumeAudio / 100f);
                    playr.PlaySong();

                }
                else
                {
                    if (grammar.Equals("Conversas"))
                    {
                        var cmd = comandos.Where(c => c.pergunta.ToLower() == command).First();
                        sintetizador.Speak(cmd.resposta);

                    }
                    else
                    {
                        if (command.StartsWith("max"))
                        {
                            string task = "";
                            task = command.Replace("max", "");
                            task.Trim();

                            if (task.StartsWith(" ligar") || task.StartsWith(" acender"))
                            {

                                foreach (Dispositivos d in mDispositivos)
                                {
                                    if (task.Contains(d.nome.ToLower()))
                                    {

                                        serialPort1.Write("1|" + d.endereco + "|" + d.saida + "|W|1");
                                        if (task.Contains("luz"))
                                        {
                                            sintetizador.Speak(d.nome + " ligada");
                                        }
                                        else
                                        {
                                            sintetizador.Speak(d.nome + " ligadas");
                                        }

                                        break;
                                    }
                                }

                            }
                            if (task.StartsWith(" desligar") || task.StartsWith(" apagar"))
                            {
                                foreach (Dispositivos d in mDispositivos)
                                {
                                    if (task.Contains(d.nome.ToLower()))
                                    {

                                        serialPort1.Write("1|" + d.endereco + "|" + d.saida + "|W|0");
                                        sintetizador.Speak(d.nome + " desligadas");
                                        break;
                                    }
                                }
                            }

                        }
                    }
                }

            }

        }

        private void comandosAnalyse(String command)
        {
            if (playr != null)
            {

                if (command.Equals("max próxima musica"))
                {
                    playr.NextSong();
                }
                else
                {
                    if (command.Equals("max parar musica"))
                    {
                        playr.StopSong();
                    }
                    else
                    {
                        if (command.Equals("max pausar musica"))
                        {
                            playr.PauseSong();
                        }
                        else
                        {
                            if (command.Equals("max continuar com a musica"))
                            {
                                playr.ResumeSong();
                            }
                            else
                            {

                                if (command.Equals("mais alto"))
                                {
                                    playr.SetVolume(true);
                                }
                                else
                                {
                                    if (command.Equals("mais baixo"))
                                    {
                                        playr.SetVolume(false);
                                    }
                                    else
                                    {
                                        if (command.StartsWith("max volume em"))
                                        {
                                            String task;
                                            task = command.Replace("max volume em", "");
                                            task = task.Trim();
                                            task = task.Substring(0, 3);
                                            task = task.TrimEnd();
                                            playr.SetVolume((float.Parse(task) / 100f));
                                            volumeAudio = int.Parse(task) / 10;
                                            trackBarAudioPlayer.Value = volumeAudio;

                                        }
                                        else
                                        {
                                            if (command.StartsWith("max musica anterior"))
                                            {
                                                playr.BackSong();
                                            }
                                            {
                                            }
                                        }

                                    }


                                }


                            }
                        }

                    }
                }
            }

            if (command.Equals("max quais artistas voce tem para tocar"))
            {
                String fileName = "";
                if (System.IO.Directory.Exists(Environment.CurrentDirectory + "\\Musicas"))
                {
                    sintetizador.Speak("Eu tenho artistas bons aqui, a começar por: ");
                    string[] files = System.IO.Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));

                    foreach (string m in files)
                    {
                        fileName = fileName + System.IO.Path.GetFileName(m) + ". ";

                    }
                    sintetizador.Speak(fileName);
                }
            }
        }

        /// <DataReceivedHandler>
        /// Recebe os dados das placas escravas e grava no arquivo send.txt
        void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            Debug.Print(indata);
            string[] stringSeparators = new string[] { "|" };
            string[] result = indata.Split(stringSeparators,
                           StringSplitOptions.RemoveEmptyEntries);

            if (result[0] == "CHECK")
            {
                Thread.Sleep(1000);
                String sql = "SELECT DISTINCT codigoDispositivo,saida FROM Dispositivos WHERE endereco=" + result[1];

                DataSet ds = database.dataSetSQL(sql);
                String comando;
                for (int i = 0; i <= ds.Tables[0].Rows.Count - 1; i++)
                {
                    comando = database.selectSQLAcesso("SELECT LAST(comando) FROM Acessos WHERE codigoDispositivo=" + ds.Tables[0].Rows[i].ItemArray[0].ToString());
                    if (comando.Length > 0)
                    {
                        //MessageBox.Show("1|" + result[1] + "|" + ds.Tables[0].Rows[i].ItemArray[1].ToString() + "|W|" + Int32.Parse(comando));
                        serialPort1.Write("1|" + result[1] + "|" + ds.Tables[0].Rows[i].ItemArray[1].ToString() + "|W|" + Int32.Parse(comando));
                        Thread.Sleep(1000);
                    }

                }

            }
            else
            {
                if (result[0] == "INTERRUPTOR" || result[0] == "STATUS")
                {

                    String codDispositivo = database.selectSQLAcesso("SELECT codigoDispositivo FROM Dispositivos WHERE saida = '" + result[2] + "' AND endereco = " + result[1]);
                    //String comando = database.selectSQLAcesso("SELECT LAST(comando) FROM Acessos WHERE codigoDispositivo=" + codDispositivo);


                    String sql = "INSERT INTO Acessos (codigoUsuario,codigoDispositivo,comando) VALUES (1,"
                                 + codDispositivo + ",'"
                                 + result[3]
                                 + "')";
                    Database.database.insertSQL(sql);
                    updateImageButton(Int32.Parse(codDispositivo), result[3]);
                    foreach (SocketT2h sk in clientSockets)
                    {
                        Sendata(sk._Socket, indata);
                    }
                }
                else
                {
                    if (result[0] == "SOS")
                    {
                        //valor.Write("Entendi que precisa de ajuda, irei comunicar aos seus parentes.");
                    }
                }
            }

            /*StreamWriter valor = new StreamWriter(@"C:\p\send.txt");
            valor.Write(indata);
            valor.Close();
            if (isSound)
            {
                valor = new StreamWriter(@"C:\p\sound.txt");
                valor.Write(indata);
                valor.Close();
                isSound=false;
            }*/

        }

        /// <Audio>
        /// 
        /// </Audio>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Audio(object sender, AudioLevelUpdatedEventArgs e)
        {
            progressBarAudioLevel.Maximum = 100;
            int value = e.AudioLevel;
            progressBarAudioLevel.Value = value;
        }
        //MÉTODOS COMANDO DE VOZ


        /// <updateImageButton>
        /// Atualiza as imagens dos botões conforme o estado das lâmpadas
        /// </updateImageButton>
        /// <param name="codDispositivo"></param>
        /// <param name="comando"></param>
        private void updateImageButton(int codDispositivo, String comando)
        {
            if (codDispositivo == 1)
            {

                if (comando.Equals("1")) btnLuzExterna.Image = Resources.btnon;
                else btnLuzExterna.Image = Resources.btnoff;

            }
            else
            {
                if (codDispositivo == 2)
                {
                    if (comando.Equals("1")) btnArandelasPisc.Image = Resources.btnon;
                    else btnArandelasPisc.Image = Resources.btnoff;
                }
                else
                {
                    if (codDispositivo == 3)
                    {
                        if (comando.Equals("1")) btnChurrasqueira.Image = Resources.btnon;
                        else btnChurrasqueira.Image = Resources.btnoff;
                    }
                }

            }
        }

        /// <timerHora_Tick>
        /// Agendamento do Controle automatico das luzes
        /// </timerHora_Tick>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timerHora_Tick(object sender, EventArgs e)
        {
            timerHora.Enabled = false;
            String sql;
            sql = "SELECT *FROM Horarios WHERE ativo = true";
            mHorarios = database.selectSQLHorarios(sql);
            lblHora.Text = DateTime.Now.ToShortTimeString();

            DateTime dateNow = DateTime.Now;

            if (mHorarios.Count > 0)
            {
                foreach (Horarios h in mHorarios)
                {
                    if (h.repetir.Equals("TD") || !h.repetir.Equals("SH"))
                    {

                        if (!h.dataInicial.Equals("NULL"))
                        {
                            DateTime dtInicial = Convert.ToDateTime(h.dataInicial);
                            if (dateNow > dtInicial)
                            {
                                exec(dateNow, h);
                            }
                        }
                        else
                        {
                            exec(dateNow, h);
                        }

                        if (!h.dataFinal.Equals("NULL"))
                        {
                            DateTime dtFinal = Convert.ToDateTime(h.dataFinal);
                            if (dateNow < dtFinal)
                            {
                                exec(dateNow, h);
                            }
                            else
                            {
                                sql = "UPDATE Horarios SET ativo=false WHERE codigoHorario=" + h.codigo;
                                database.updateSQL(sql);
                            }
                        }
                        else
                        {
                            exec(dateNow, h);
                        }

                    }
                    else
                    {
                        sql = "SELECT *FROM Dispositivos WHERE codigoDispositivo = " + h.codigoDispositivo;
                        Dispositivos disp = database.selectSQLDispositivo(sql);
                        serialPort1.Write("1|" + disp.endereco + "|" + disp.saida + "|W|" + h.comando);
                        sql = "UPDATE Horarios SET ativo=false WHERE codigoHorario=" + h.codigo;
                        database.updateSQL(sql);
                    }
                }
            }

            timerHora.Enabled = true;
        }

        private void exec(DateTime dateNow, Horarios h)
        {
            String sql;
            DateTime dtHorario = Convert.ToDateTime(h.horario);
            DateTime dtAuxiliar = Convert.ToDateTime(h.dataAuxiliar);
            if (dateNow >= dtAuxiliar)
            {

                if (dateNow > dtHorario)
                {
                    sql = "SELECT *FROM Dispositivos WHERE codigoDispositivo = " + h.codigoDispositivo;
                    Dispositivos disp = database.selectSQLDispositivo(sql);

                    if (h.repetir.Equals("TD"))
                    {
                        serialPort1.Write("1|" + disp.endereco + "|" + disp.saida + "|W|" + h.comando);
                        sql = "UPDATE Horarios SET dataAuxiliar= '" + dateNow.AddDays(1).ToShortDateString() + "' WHERE codigoHorario=" + h.codigo;
                        database.updateSQL(sql);
                        h.dataAuxiliar = "" + dateNow.AddDays(1);
                    }
                    else
                    {

                        string[] stringSeparators = new string[] { "|" };
                        string[] repetir = h.repetir.Split(stringSeparators,
                                       StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < repetir.Count(); i++)
                        {
                            if ((int)dateNow.DayOfWeek == int.Parse(repetir[i]))
                            {
                                serialPort1.Write("1|" + disp.endereco + "|" + disp.saida + "|W|" + h.comando);
                                sql = "UPDATE Horarios SET dataAuxiliar= '" + dateNow.AddDays(1).ToShortDateString() + "' WHERE codigoHorario=" + h.codigo;
                                database.updateSQL(sql);
                            }
                        }
                    }
                }
            }
        }

        private void conectarToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void statusConexao_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            List<Acessos> listaAcessos;
            DateTime datevalue1 = new DateTime();
            listaAcessos = database.selectSQLAcessos("SELECT *FROM Acessos WHERE Dispositivo='lamp1' AND Horario like '30/12/2015%'");
            bool ck = false;
            double horas = 0;
            foreach (Acessos acessos in listaAcessos)
            {
                if (acessos.comando.Equals("1") && !ck)
                {
                    datevalue1 = (Convert.ToDateTime(acessos.horario));
                    ck = true;
                }

                if (acessos.comando.Equals("0") && ck)
                {
                    DateTime datevalue2 = (Convert.ToDateTime(acessos.horario));
                    TimeSpan span = datevalue2.Subtract(datevalue1);
                    horas = horas + span.TotalHours;
                    ck = false;
                }

            }
            double potencia = 6;
            potencia = potencia / 1000;
            double potenciaDia = potencia * horas;

            Console.WriteLine("FIM: " + potenciaDia * 30 * 0.57069801);
        }

        private void consumoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Relatorio rlt = new Relatorio();
            rlt.ShowDialog();
        }


        private void btnSpeech_Click(object sender, EventArgs e)
        {


        }

        /// <previsao>
        /// Informa a previsão do tempo para o dia, próximo dia ou final de semana
        /// conforme  a requisição inserida na variável Array "auxiliar" de 3 posições:
        /// auxiliar[0] -> PREVISAO (Indica a requisição da previsão)
        /// auxiliar[1] -> Informa DIA ou FIMDESEMANA, caso DIA recorre a auxiliar[2]
        /// auxiliar[2] -> Caso "1" = Se refere ao dia atual(Hoje)
        ///                Caso "2" = Se refere ao próximo dia(Amanhã)
        ///                OBS: Não necessário para requisição do fim de semana
        ///                
        /// Exemplo para previsão de amanhã:
        /// PREVISAO|DIA|2
        /// 
        /// </summary>
        /// <param name="list"></param> Contém as respostas da previsão
        private void previsao(List<String> list)
        {

            if (list != null)
            {
                int index = Int32.Parse(auxiliar[2]);
                String resposta = "";
                if (auxiliar[1].Equals("DIA"))
                {
                    if (auxiliar[2].Equals("1"))
                    {
                        resposta = "A previsão para hoje é: " + list[index - 1];

                    }
                    else
                    {
                        if (auxiliar[2].Equals("2"))
                        {
                            resposta = "A previsão para amanhã é: " + list[index - 1];
                        }
                    }
                }
                else
                {
                    resposta = "A previsão para o fim de semana é: Sábado: " + list[index] + " e Domingo, " + list[index + 1];
                }
                sintetizador.SpeakAsync(resposta);

            }
            else
            {
                sintetizador.SpeakAsync("Desculpe Senhor, estou sem conecção com a internet");

            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            //String Comando=database.selectSQLAcesso("SELECT LAST(Comando) FROM Acessos WHERE Dispositivo='Saida1'");
            //checkMac("E4:90:7E:7A:2F:5C");
            //checkDispositivos("Arandelas1",3);
            //Debug.Write("1|3|Saida1|W|"+Int32.Parse(Comando));
            //serialPort1.Write("1|3|Saida1|W|"+Int32.Parse(Comando));
            //  database.selectSQLAcesso("SELECT Comando FROM Acessos WHERE Dispositivo='Saida1' ORDER BY Codigo DESC");
        }

        private void usuáriosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CadastroUsuarios rlt = new CadastroUsuarios();
            rlt.ShowDialog();
        }

        private void cadastrarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CadastroUsuarios rlt = new CadastroUsuarios();
            rlt.ShowDialog();
        }

        private void editarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EditarUsuarios rlt = new EditarUsuarios();
            rlt.ShowDialog();
        }

        private void cadastrarToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            CadastroDispositivos rlt = new CadastroDispositivos();
            rlt.ShowDialog();
        }

        private void gerenciarToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            GerenciarDispositivos rlt = new GerenciarDispositivos();
            rlt.ShowDialog();
        }

        private void btnArandelasPisc_Click(object sender, EventArgs e)
        {
            serialPort1.Write("1|3|Saida2|W|IS");
            //serialPort1.Write("1|3|Saida1|W|IS");
            //sendCommand("Saida2", 3, 3,btnArandelasPisc);
        }

        private void btnChurrasqueira_Click(object sender, EventArgs e)
        {
            serialPort1.Write("1|3|Saida3|W|IS");

        }

        private void button9_Click(object sender, EventArgs e)
        {
            serialPort1.Write("1|3|Saida1|W|IS");
            // sendCommand("Saida1",3,2,btnLuzExterna);

        }

        private void button1_Click_1(object sender, EventArgs e)
        {

        }

        private void btnPisc_Click(object sender, EventArgs e)
        {

            serialPort1.Write("1|3|Saida1|W|IS");
        }

        private void btnEntrada_Click(object sender, EventArgs e)
        {
            serialPort1.Write("1|3|S|W|T1");
        }

        private void btnGaragem_Click(object sender, EventArgs e)
        {
            serialPort1.Write("1|3|S|W|T0");
        }

        private void btnRol_Click(object sender, EventArgs e)
        {
            String sql = "UPDATE Horarios SET ativo=false";
            database.updateHorario(sql);
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ControleAutomatico rlt = new ControleAutomatico();
            rlt.ShowDialog();

        }

        private void button5_Click(object sender, EventArgs e)
        {


        }

        public void AddDataMethod(String myString)
        {
        }

        public static void Worker(object sender, object ev)
        {
            SerialDataReceivedEventArgs e = (SerialDataReceivedEventArgs)ev;
            // Put your code here
        }

        /*public void SubMenu(ToolStripMenuItem MnuItems, string var)
        {
            if (var == "File")
            {
                string[] row = new string[] { "New", "Open", "Add", "Close", "Close Solution" };
                foreach (string rw in row)
                {
                    ToolStripMenuItem SSMenu = new ToolStripMenuItem(rw, null, ChildClick);
                    SubMenu(SSMenu, rw);
                    MnuItems.DropDownItems.Add(SSMenu);
                }
            }

            if (var == "New")
            {
                string[] row = new string[] { "Project", "Web Site", "File..", "Project From Existing Code" };
                foreach (string rw in row)
                {
                    ToolStripMenuItem SSSMenu = new ToolStripMenuItem(rw, null, ChildClick);
                    MnuItems.DropDownItems.Add(SSSMenu);
                }
            }
        }*/

        public void ChildClick(object sender, System.EventArgs e)
        {
            MessageBox.Show(string.Concat("You have Clicked '", sender.ToString(), "' Menu"), "Menu Items Event",
                                                                         MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void sairToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void telaCheiaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (telaCheia)
            {
                telaCheiaToolStripMenuItem.Text = "Tela Cheia";
                GoFullscreen(false);

            }
            else
            {
                telaCheiaToolStripMenuItem.Text = "Sair Tela Cheia";
                GoFullscreen(true);

            }

        }

        private void GoFullscreen(bool fullscreen)
        {
            if (fullscreen)
            {
                telaCheia = true;
                this.WindowState = FormWindowState.Normal;
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                this.Bounds = Screen.PrimaryScreen.Bounds;
            }
            else
            {
                telaCheia = false;
                this.WindowState = FormWindowState.Maximized;
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            }
        }

        private void trackBarAudioPlayer_Scroll(object sender, EventArgs e)
        {
            volumeAudio = int.Parse(trackBarAudioPlayer.Value.ToString());

            if (playr != null)
            {
                playr.SetVolume(volumeAudio / 100f);


            }

        }



        private void btnFimDeSemana_Click(object sender, EventArgs e)
        {
            StreamWriter valor = new StreamWriter(@"C:\p\read.txt");
            valor.Write("PREVISAO|FIMDESEMANA|0");
            valor.Close();
        }

        private void btnAmanha_Click(object sender, EventArgs e)
        {
            StreamWriter valor = new StreamWriter(@"C:\p\read.txt");
            valor.Write("PREVISAO|DIA|2");
            valor.Close();
            //MessageBox.Show(""+volumeAudio);
            foreach (SocketT2h st in clientSockets)
            {
                MessageBox.Show("S: " + st._Socket + "| N: " + st._Name);
            }
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            //MessageBox.Show(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
            //StreamWriter valor = new StreamWriter(@"C:\p\read.txt");
            //// valor.Write("PREVISAO|DIA|1");
            //valor.Close();
        }

        private void toolStripStatusLabel1_Click(object sender, EventArgs e)
        {

        }

        private void lblHora_Click(object sender, EventArgs e)
        {

        }

        private void button3_Click_1(object sender, EventArgs e)
        {

            foreach (SocketT2h st in clientSockets)
            {
                MessageBox.Show("S: " + st._Socket + "| N: " + st._Name);
            }
        }



        public void changeMusicListenner(string music)
        {
            String send;
            if (music.Equals("STOP"))
            {
                send = "PLAYER|STATUS|STOP";
                foreach (SocketT2h st in clientSockets)
                {
                    if (st._Screen != null && st._Screen.Equals("PLAYER"))
                    {
                        Sendata(st._Socket, send);
                        break;
                    }
                }
            }
            else
            {
                send = playr.howPlaying();
                send = send + "|VOLUME|" + volumeAudio;
                foreach (SocketT2h st in clientSockets)
                {
                    if (st._Screen != null && st._Screen.Equals("PLAYER"))
                    {
                        Sendata(st._Socket, send);
                        break;
                    }
                }
            }
        }

        private void trackBarAudioPlayer_MouseUp(object sender, MouseEventArgs e)
        {

            if (playr != null)
            {
                foreach (SocketT2h st in clientSockets)
                {
                    if (st._Screen != null && st._Screen.Equals("PLAYER"))
                    {
                        float v = playr.getVolume() * 100f;
                        Sendata(st._Socket, "PLAYER|STATUS|VOLUME|" + v);

                        break;
                    }
                }
            }
        }
    }
}
