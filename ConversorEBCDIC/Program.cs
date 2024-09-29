//#define  VERSION_PRODUCCION
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using static System.Net.WebRequestMethods;
using System.Security.Cryptography;

namespace ConversorEBCDIC
{
    public class Program
    {
        static string pathLog;
        static string nombreLog;
        public static string saltoLinea;
        public static bool hayInicioRegistro = false; 
        public static bool hayLog = false;
        static void Main(string[] args)
        {

            EscribirLog("Entra en conversor 0");
            string[] formatosAceptados = { "EBCDIC-CP284", "WIN-1252" };

            // leer todas las variables de entorno necesarias
            pathLog = ConfigurationManager.AppSettings["pathLog"];
            nombreLog = ConfigurationManager.AppSettings["nameLog"];
            saltoLinea = ConfigurationManager.AppSettings["saltoLinea"].ToUpper();
            if (ConfigurationManager.AppSettings["hayInicioRegistro"].ToUpper().Equals("TRUE"))
                hayInicioRegistro = true;
            else
                hayInicioRegistro = false;
            if (ConfigurationManager.AppSettings["hayLog"].Equals("true"))
                hayLog = true;

            EscribirLog("Entra en conversor 1");

            //const int TAM = 300;
            //byte[] buffer = new byte[TAM];
            //int bytesRead;

            //using (Stream stdin = Console.OpenStandardInput())
            //{
            //    while ((bytesRead = stdin.Read(buffer, 0, TAM)) == recsz) //NO, tengo que leer el archivo completo no se el tamaño
            //    {
            //        // Unpack fields specified using TYPEDEF in array
            //        string field1 = Encoding.Default.GetString(buffer, 0, 4);
            //        string field2 = Encoding.Default.GetString(buffer, 4, 20);
            //        string field3 = Encoding.Default.GetString(buffer, 24, 2);
            //        string field4 = Encoding.Default.GetString(buffer, 26, 20);
            //        string field5 = Encoding.Default.GetString(buffer, 46, 60);
            //        string field6 = Encoding.Default.GetString(buffer, 106, 48);

            //        // Convert each EBCDIC string to ASCII
            //        string s1 = EbcdicToAscii(field2);
            //        string s2 = EbcdicToAscii(field4);
            //        string s3 = EbcdicToAscii(field5);

            //        // Pack data using unchanged fields from array for binary and
            //        // converted EBCDIC strings where appropriate
            //        byte[] output = new byte[recsz];
            //        Array.Copy(buffer, 0, output, 0, 4); // field1
            //        Array.Copy(Encoding.Default.GetBytes(s1), 0, output, 4, s1.Length);
            //        Array.Copy(buffer, 24, output, 24, 2); // field3
            //        Array.Copy(Encoding.Default.GetBytes(s2), 0, output, 26, s2.Length);
            //        Array.Copy(Encoding.Default.GetBytes(s3), 0, output, 46, s3.Length);
            //        Array.Copy(buffer, 106, output, 106, 48); // field6

            //        Console.OpenStandardOutput().Write(output, 0, output.Length);
            //    }
            //}

            string archivoOrigen=string.Empty;
            string archivoDestino = string.Empty;
            string archivoCpy = string.Empty;
            string formatoInicial = string.Empty;
            string formatoFinal = string.Empty;
            string longitud = string.Empty;

            // EN LA VERSION FINAL INCLUIR ESTO
#if VERSION_PRODUCCION
            // analizar los parametros
            for (int i = 0; i < args.Count(); i++)
            {
                Console.WriteLine(args[i]);
                if (args[i].Equals("-i"))
                    archivoOrigen = args[i + 1];
                else if (args[i].Equals("-o"))
                    archivoDestino = args[i + 1];
                else if (args[i].Equals("-c"))
                    archivoCpy = args[i + 1];
                else if (args[i].Equals("-oe"))
                    formatoInicial = args[i + 1];
                else if (args[i].Equals("-de"))
                    formatoFinal = args[i + 1];
                else if (args[i].Equals("-l"))
                    longitud = args[i + 1];

                // El resto de los campos/formatos los ignoramos
            }
            //Console.ReadLine();

            EscribirLog("archivoOrigen: " + archivoOrigen);
            EscribirLog("archivoDestino: " + archivoDestino);
            EscribirLog("archivoCpy: " + archivoCpy);
            EscribirLog("formatoInicial: " + formatoInicial);
            EscribirLog("formatoFinal: " + formatoFinal);
            EscribirLog("longitud: " + longitud);

            //comprobar formatos
            if (!formatosAceptados.Any(formatoInicial.Contains))
            {
                EscribirLog("La llamada incluye un formato inicial no aceptado");
                return;
            }

            if (!formatosAceptados.Any(formatoFinal.Contains))
            {
                EscribirLog("La llamada incluye un formato final no aceptado");
                return;
            }
            if (formatoFinal.Equals(formatoInicial))
            {
                EscribirLog("El formato inicial y final es el mismo. No se hace ninguna transformación");
                return;
            }

            // FIN DE EN LA VERSION FINAL INCLUIR ESTO
#else
            archivoOrigen = @"M:\desarrollo\PROJECTS\Moisesc\tablasDESCARGADAS\BI11.MIGRA.A0000031.ONLINE.COMP0002.BIN";
            archivoDestino = @"M:\desarrollo\PROJECTS\Moisesc\tablasDESCARGADAS\BI11.MIGRA.A0000031.ONLINE.COMP0002.DAT";
            archivoCpy = @"M:\desarrollo\PROJECTS\Moisesc\tablasDESCARGADAS\BDCOM1US.cpy";
            longitud = "";
            formatoInicial = "EBCDIC-CP284";
            formatoFinal = "WIN-1252";

#endif

            ConvertEBCDIC.ConvertirFichero(archivoOrigen, archivoDestino, archivoCpy, formatoInicial, formatoFinal, longitud);
            EscribirLog("Ha convertido");
        }
        public static void EscribirLog(string texto)
        {
            if (!hayLog)
                return;
            try
            {

                if (!System.IO.File.Exists(nombreLog))
                {
                    System.IO.File.Create(nombreLog);
                }
                //StreamWriter sw = new StreamWriter(pathLog + "\\" + nombreLog, true, Encoding.ASCII);
                StreamWriter sw = new StreamWriter(nombreLog, true, Encoding.ASCII);
                sw.Write(DateTime.Now.ToString() + " " + texto + Environment.NewLine);
                sw.Close();
            }
            catch (Exception e)
            {
            }
        }

    }
}
