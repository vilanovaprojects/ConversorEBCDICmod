using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace ConversorEBCDIC
{
    public  class EBCDIC_Windows
    {
        public byte EBCDIC { get; set; }
        public byte Windows { get;set; }

        public EBCDIC_Windows(byte _EBCDIC, byte _Windows) { EBCDIC = _EBCDIC; Windows = _Windows; }
    }

    //diferencio porque en Windows hay menos caracteres que transformar y tengo ambiguedades: EBCDIC 48 puede ser 48 o 240
    //en Windows solo tengo la relación 240 y si uso la misma tabla, me coje el 48 que no me interesa

    public class Windows_EBCDIC
    {
        public byte EBCDIC { get; set; }
        public byte Windows { get; set; }
        public Windows_EBCDIC(byte _Windows, byte _EBCDIC) { Windows = _Windows; EBCDIC = _EBCDIC; }


    }
    // Estas dos clases son para leer el archivo Cpy
    public class CampoTabla
    {
        public string Nombre { get; set; }
        //public string Tipo { get; set; }
        public bool Transformar { get; set; }
        public bool Copiar { get; set; }
        public long Tamano { get; set; }
        public long PosicionInicial { get; set; }
        public long PosicionFinal { get; set; }
    }
    public class DatosTabla
    {
        public string NombreTabla { get; set; }
        public long LongitudRegistro { get; set; }
        public List<CampoTabla> ListaCampos { get; set; }

    }

    public static class ConvertEBCDIC
    {
        static List<EBCDIC_Windows> ListaConversion;
        static List<Windows_EBCDIC> ListaConversionWindows_EBCDIC;
        static bool hayFinalArchivo = false;

        public static void LeerFichero(string archivoOrigen, string archivoDestino)
        {
            //FileStream streamOrigen = null;
            ////FileStream streamDestino = null;

            //streamOrigen = new FileStream(archivoOrigen, FileMode.Open, FileAccess.Read);
            ////streamDestino = new FileStream(archivoDestino, FileMode.Append);

            //StreamWriter sw = new StreamWriter(archivoDestino, true, Encoding.ASCII);

            ////byte[] cadena = new byte[int.MaxValue / 8];
            //int longitud = Convert.ToInt32(streamOrigen.Length);
            //byte[] cadena = new byte[longitud + 10];
            //streamOrigen.Read(cadena, 0, longitud);

            //// string[] cadenaDestino = new string[int.MaxValue / 4];
            //string[] cadenaDestino = new string[longitud * 4];

            ////for (int i = 0; i < longitud; i ++)
            ////{
            //string valor = ByteArrayToString(cadena);
            //sw.Write(valor);

            ////}

            //streamOrigen.Close();
            //sw.Close();



        }


        public static string ByteArrayToString(byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
        }

        public static void ConvertirFichero(string archivoOrigen, string archivoDestino, string archivoCpy, string formatoOrigen, string formatoDestino, string longitud)
        {
            Program.EscribirLog("Parametros");
            Program.EscribirLog("archivoOrigen:" + archivoOrigen);
            Program.EscribirLog("archivoDestino:" + archivoDestino);
            Program.EscribirLog("archivoCpy:" + archivoCpy);
            Program.EscribirLog("formatoOrigen:" + formatoOrigen);
            Program.EscribirLog("formatoDestino: " + formatoDestino);
            Program.EscribirLog("longitud: " + longitud);

            //Si el fichero destino existe hay que borrarlo
            if (File.Exists(archivoDestino))
                File.Delete(archivoDestino);

            //Info del CPY
            DatosTabla cpy = LeerJsonCpy(archivoCpy);
            int TamRegistro = (int)cpy.LongitudRegistro;

            // Info del archivo
            FileInfo archivoInfo = new FileInfo(archivoOrigen);
            long tamañoArchivo = archivoInfo.Length;
            int limite = 300000000;


            try
            {
                //if el tamañoArchivo no es divisible por la longitud de la fila algo anda mal...
                if (tamañoArchivo % TamRegistro != 0)
                {
                    throw new Exception("La división entre la longitud del archivo y entre longreg no es exacta");
                }

                if (tamañoArchivo < limite)
                {
                    //Abrir arreglo
                    byte[] arregloBytes = File.ReadAllBytes(archivoOrigen);


                    //Procesado POR FILAS
                    int repeticiones = (int)(tamañoArchivo / TamRegistro);
                    int j = 0;

                    for (int i = 0; i < repeticiones; i++)
                    {
                        foreach (CampoTabla ct in cpy.ListaCampos)
                        {
                            if (ct.Transformar == true)
                            {
                                for (int z = 0; z < ct.Tamano; z++)
                                {
                                    arregloBytes[j] = conEBCDICoptimo(arregloBytes[j]);
                                    j++;
                                }
                            }
                            else
                            {
                                j += (int)ct.Tamano;
                            }
                        }
                    }
                    // GUARDAR arregloBytes
                    File.WriteAllBytes(archivoDestino, arregloBytes);

                }
                else
                {
                    // PRIMER BLOQUE (Primer número menor a limite pero multiplo de TamRegistro)
                    int cociente = limite / TamRegistro;
                    int tamañoBloque = cociente * TamRegistro;
                    long bytesProcesados = 0;
                    long offset = 0; //Posición del lector del filestream

                    using (FileStream fsOrigen = new FileStream(archivoOrigen, FileMode.Open, FileAccess.Read))
                    using (FileStream fsDestino = new FileStream(archivoDestino, FileMode.Create, FileAccess.Write))
                    {

                        while (bytesProcesados < tamañoArchivo)
                        {
                            // If estamos en el ÚLTIMO BLOQUE, ajustar el tamaño si es necesario
                            if (bytesProcesados + tamañoBloque > tamañoArchivo)
                            {
                                tamañoBloque = (int)(tamañoArchivo - bytesProcesados); // último bloque es pequeño, se puede convertir a INT                    
                            }


                            // NºFILAS=numreps BLOQUE=arreglobytes y ponemos contador de bytes a 0
                            int numreps = tamañoBloque / TamRegistro;
                            byte[] arreglobytes = new byte[tamañoBloque];
                            int numbyte = 0;


                            //Cargamos arreglobytes desde el FILESTREAM. Primera lectura offset = 0
                            fsOrigen.Seek(offset, SeekOrigin.Begin);  //Desplazamiento del lector
                            fsOrigen.Read(arreglobytes, 0, tamañoBloque); //carga de arreglobytes

                            //Leemos por filas
                            for (int i = 0; i < numreps; i++)
                            {
                                //Leemos por campo(por cada campo de fila)
                                foreach (CampoTabla ct in cpy.ListaCampos)
                                {
                                    if (ct.Transformar == true)
                                    {
                                        //Transformamos todos los bytes del campo
                                        for (int z = 0; z < ct.Tamano; z++)
                                        {
                                            arreglobytes[numbyte] = conEBCDICoptimo(arreglobytes[numbyte]);
                                            numbyte++;
                                        }
                                    }
                                    else
                                    {
                                        //saltamos campo
                                        numbyte += (int)ct.Tamano;
                                    }

                                }
                            }

                            // Desplazamos el lector de FileStream al byte tamañoBLoque
                            offset += tamañoBloque;

                            // Escribir el bloque procesado en el archivo de destino
                            fsDestino.Write(arreglobytes, 0, tamañoBloque);

                            // Actualizar el contador de bytes procesados
                            bytesProcesados += tamañoBloque;
                        }
                    }


                }
            }

            catch (Exception e)
            {
                Program.EscribirLog("ConvertirFichero: Exception " + e.Message);

            }





        }
        
        public static DatosTabla LeerJsonCpy(string archivoCpy)
        {
            string Json = string.Empty;

            using (TextReader readertext = new StreamReader(archivoCpy))// , Encoding.GetEncoding("windows-1252")))
            {
                Json = readertext.ReadToEnd();

            }
            DatosTabla cpy = JsonConvert.DeserializeObject<DatosTabla>(Json);

            return cpy;

        }

        // En una versión de archivo que he recibido, al final del archivo viene un SUB como si fuera final 
        // de archivo, pero no lo tengo que contabilizar como final de archivo.
        // sin embargo, si hay que añadirlo al final del archivo transformado
        //No tengo saltos de línea
        public static long CalcularTotalRegistros(string archivo, bool EbcdicAWindows)
        {
            int TotalRegistros = 0;

            hayFinalArchivo = false;
            try
            {
                String linea;
                StreamReader file = new StreamReader(archivo);
                while ((linea = file.ReadLine()) != null)
                //while ((file.ReadLine()) != null)
                {
                    if (EbcdicAWindows)
                    {
                        if (linea[0] != 63)
                            TotalRegistros++;
                        else
                            hayFinalArchivo = true;
                    }
                    else
                    {
                        if (linea[0] != 26)
                            TotalRegistros++;
                        else
                            hayFinalArchivo = true;
                    }
                }

            }
            catch (Exception e)
            {
                Program.EscribirLog("CalcularTotalRegistros: Exception " + e.Message);
            }
            return TotalRegistros;

        }

        public static byte[] LeeLoQueFaltaDeLinea(FileStream streamOrigen, ref long faltaban)
        {
            // TODO: poner un tamaño que permita recoger todo 
            byte[] resto = new byte[Int32.MaxValue / 4000]; // esto no es la mejor solución

            int i = -1;
            do
            {
                i++;
                streamOrigen.Read(resto, i, 1);

            } while (resto[i] != 13);

            // quitarle el último leido. Retroceder         
            streamOrigen.Seek(streamOrigen.Position - 1, SeekOrigin.Begin);
            faltaban = i;
            resto[i] = 0;
            return resto;
        }




        //Poner esto para utilizar esta función: arreglobytes[i] = conEBCDICoptimo(arreglobytes[i]);
        //byte[] arreglobytes = File.ReadAllBytes(archivoOrigen); y File.WriteAllBytes(archivoDestino, arreglobytes); 
        public static byte conEBCDICoptimo(byte data)
        {
            // Extraer los primeros 4 bits
            byte primerosCuatroBits = (byte)(data & 0xF0);
            byte output = data;

            switch (primerosCuatroBits)
            {
                
                case 0x00: //00000000000000000000000000000000000000000000000000000000000000000

                    switch (data)
                    {
                        case 0x00:
                            output = 0x00;     //00 --> 00
                            break;
                        case 0x01:
                            output = 0x01;     //01 --> 01
                            break;
                        case 0x02:
                            output = 0x02;     //02 --> 02
                            break;
                        case 0x03:
                            output = 0x03;     //03 --> 03
                            break;
                        case 0x04:
                            output = 0x04;     //04 --> 04
                            break;
                        case 0x05:
                            output = 0x09;     //05 --> 09
                            break;
                        case 0x06:
                            output = 0x06;     //06 --> 06
                            break;
                        case 0x07:
                            output = 0x7F;     //07 --> 7F
                            break;
                        case 0x08:
                            output = 0x08;     //08 --> 08
                            break;
                        case 0x09:
                            output = 0x09;     //09 --> 09
                            break;
                        case 0x0A:
                            output = 0x0A;     //0A --> 0A
                            break;
                        case 0x0B:
                            output = 0x0B;     //0B --> 0B
                            break;
                        case 0x0C:
                            output = 0x0C;     //0C --> 0C
                            break;
                        case 0x0D:
                            output = 0x0D;     //0D --> 0D
                            break;
                        case 0x0E:
                            output = 0x0E;     //0E --> 0E
                            break;
                        case 0x0F:
                            output = 0x0F;     //0F --> 0F
                            break;
                    }
                    break;

                case 0x40: //44444444444444444444444444444444444444444444444444444444444444444

                    switch (data)
                    {
                        case 0x40:
                            output = 0x20;     // 40 --> 20
                            break;
                        case 0x41:
                            output = 0xA0;     // 41 --> A0
                            break;
                        case 0x42:
                            output = 0xE2;     // 42 --> E2
                            break;
                        case 0x43:
                            output = 0xE4;     // 43 --> E4
                            break;
                        case 0x44:
                            output = 0xE0;     // 44 --> E0
                            break;
                        case 0x45:
                            output = 0xE1;     // 45 --> E1
                            break;
                        case 0x46:
                            output = 0xE3;     // 46 --> E3
                            break;
                        case 0x47:
                            output = 0xE5;     // 47 --> E5
                            break;
                        case 0x48:
                            output = 0xE7;     // 48 --> E7
                            break;
                        case 0x49:
                            output = 0xA6;     // 49 --> A6
                            break;
                        case 0x4A:
                            output = 0x5B;     // 4A --> 5B
                            break;
                        case 0x4B:
                            output = 0x2E;     // 4B --> 2E
                            break;
                        case 0x4C:
                            output = 0x3C;     // 4C --> 3C
                            break;
                        case 0x4D:
                            output = 0x28;     // 4D --> 28
                            break;
                        case 0x4E:
                            output = 0x2B;     // 4E --> 2B
                            break;
                        case 0x4F:
                            output = 0x7C;     // 4F --> 7C
                            break;

                    }
                    break;

                case 0x10: //11111111111111111111111111111111111111111111111111111111111111111

                    switch (data)
                    {
                        case 0x10:
                            output = 0x10;     // 10 --> 10
                            break;                       
                        case 0x11:                       
                            output = 0x11;     // 11 --> 11
                            break;                       
                        case 0x12:                       
                            output = 0x12;     // 12 --> 12
                            break;                       
                        case 0x13:                       
                            output = 0x13;     // 13 --> 13
                            break;                       
                        case 0x14:                       
                            output = 0x14;     // 14 --> 14
                            break;                       
                        case 0x15:                       
                            output = 0x85;     // 15 --> 85
                            break;                       
                        case 0x16:                       
                            output = 0x08;     // 16 --> 08
                            break;                       
                        case 0x17:                       
                            output = 0x17;     // 17 --> 17
                            break;                       
                        case 0x18:                       
                            output = 0x18;     // 18 --> 18
                            break;                       
                        case 0x19:                       
                            output = 0x19;     // 19 --> 19
                            break;                       
                        case 0x1A:                       
                            output = 0x1A;     // 1A --> 1A
                            break;                       
                        case 0x1B:                       
                            output = 0x1B;     // 1B --> 1B
                            break;                       
                        case 0x1C:                       
                            output = 0x1C;     // 1C --> 1C
                            break;                       
                        case 0x1D:                       
                            output = 0x1D;     // 1D --> 1D
                            break;                       
                        case 0x1E:                       
                            output = 0x1E;     // 1E --> 1E
                            break;                       
                        case 0x1F:                       
                            output = 0x1F;     // 1F --> 1F
                            break;
                    }
                    break;

                case 0x20: //22222222222222222222222222222222222222222222222222222222222222222

                    switch (data)
                    {
                        case 0x20:
                            output = 0x20;     // 20 --> 20
                            break;
                        case 0x21:
                            output = 0x21;     // 21 --> 21
                            break;
                        case 0x22:
                            output = 0x22;     // 22 --> 22
                            break;
                        case 0x23:
                            output = 0x23;     // 23 --> 23
                            break;
                        case 0x24:
                            output = 0x24;     // 24 --> 24
                            break;
                        case 0x25:
                            output = 0x0A;     // 25 --> 0A
                            break;
                        case 0x26:
                            output = 0x17;     // 26 --> 17
                            break;
                        case 0x27:
                            output = 0x1B;     // 27 --> 1B
                            break;
                        case 0x28:
                            output = 0x28;     // 28 --> 28
                            break;
                        case 0x29:
                            output = 0x29;     // 29 --> 29
                            break;
                        case 0x2A:
                            output = 0x2A;     // 2A --> 2A
                            break;
                        case 0x2B:
                            output = 0x2B;     // 2B --> 2B
                            break;
                        case 0x2C:
                            output = 0x2C;     // 2C --> 2C
                            break;
                        case 0x2D:
                            output = 0x05;     // 2D --> 05
                            break;
                        case 0x2E:
                            output = 0x2E;     // 2E --> 2E
                            break;
                        case 0x2F:
                            output = 0x07;     // 2F --> 07
                            break;

                    }
                    break;

                case 0x30: //33333333333333333333333333333333333333333333333333333333333333333

                    switch (data)
                    {
                        case 0x30:
                            output = 0x30;     // 30 --> 30
                            break;
                        case 0x31:
                            output = 0x31;     // 31 --> 31
                            break;
                        case 0x32:
                            output = 0x16;     // 32 --> 16
                            break;
                        case 0x33:
                            output = 0x33;     // 33 --> 33
                            break;
                        case 0x34:
                            output = 0x34;     // 34 --> 34
                            break;
                        case 0x35:
                            output = 0x35;     // 35 --> 35
                            break;
                        case 0x36:
                            output = 0x36;     // 36 --> 36
                            break;
                        case 0x37:
                            output = 0x04;     // 37 --> 04
                            break;
                        case 0x38:
                            output = 0x38;     // 38 --> 38
                            break;
                        case 0x39:
                            output = 0x39;     // 39 --> 39
                            break;
                        case 0x3A:
                            output = 0x3A;     // 3A --> 3A
                            break;
                        case 0x3B:
                            output = 0x3B;     // 3B --> 3B
                            break;
                        case 0x3C:
                            output = 0x14;     // 3C --> 14
                            break;
                        case 0x3D:
                            output = 0x15;     // 3D --> 15
                            break;
                        case 0x3E:
                            output = 0x3E;     // 3E --> 3E
                            break;
                        case 0x3F:
                            output = 0x1A;     // 3F --> 1A
                            break;


                    }
                    break;

                case 0xF0: //FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF

                    switch (data)
                    {
                        case 0xF0:
                            output = 0x30;     // F0 --> 30
                            break;
                        case 0xF1:
                            output = 0x31;     // F1 --> 31
                            break;
                        case 0xF2:
                            output = 0x32;     // F2 --> 32
                            break;
                        case 0xF3:
                            output = 0x33;     // F3 --> 33
                            break;
                        case 0xF4:
                            output = 0x34;     // F4 --> 34
                            break;
                        case 0xF5:
                            output = 0x35;     // F5 --> 35
                            break;
                        case 0xF6:
                            output = 0x36;     // F6 --> 36
                            break;
                        case 0xF7:
                            output = 0x37;     // F7 --> 37
                            break;
                        case 0xF8:
                            output = 0x38;     // F8 --> 38
                            break;
                        case 0xF9:
                            output = 0x39;     // F9 --> 39
                            break;
                        case 0xFA:
                            output = 0xB3;     // FA --> B3
                            break;
                        case 0xFB:
                            output = 0xDB;     // FB --> DB
                            break;
                        case 0xFC:
                            output = 0xDC;     // FC --> DC
                            break;
                        case 0xFD:
                            output = 0xD9;     // FD --> D9
                            break;
                        case 0xFE:
                            output = 0xDA;     // FE --> DA
                            break;
                        case 0xFF:
                            output = 0xFF;     // FF --> FF
                            break;


                    }
                    break;

                case 0x80: //88888888888888888888888888888888888888888888888888888888888888888

                    switch (data)
                    {
                        case 0x80:
                            output = 0xD8;     // 80 --> D8
                            break;
                        case 0x81:
                            output = 0x61;     // 81 --> 61
                            break;
                        case 0x82:
                            output = 0x62;     // 82 --> 62
                            break;
                        case 0x83:
                            output = 0x63;     // 83 --> 63
                            break;
                        case 0x84:
                            output = 0x64;     // 84 --> 64
                            break;
                        case 0x85:
                            output = 0x65;     // 85 --> 65
                            break;
                        case 0x86:
                            output = 0x66;     // 86 --> 66
                            break;
                        case 0x87:
                            output = 0x67;     // 87 --> 67
                            break;
                        case 0x88:
                            output = 0x68;     // 88 --> 68
                            break;
                        case 0x89:
                            output = 0x69;     // 89 --> 69
                            break;
                        case 0x8A:
                            output = 0xAB;     // 8A --> AB
                            break;
                        case 0x8B:
                            output = 0xBB;     // 8B --> BB
                            break;
                        case 0x8C:
                            output = 0xF0;     // 8C --> F0
                            break;
                        case 0x8D:
                            output = 0xFD;     // 8D --> FD
                            break;
                        case 0x8E:
                            output = 0xFE;     // 8E --> FE
                            break;
                        case 0x8F:
                            output = 0xB1;     // 8F --> B1
                            break;

                    }
                    break;

                case 0x90: //99999999999999999999999999999999999999999999999999999999999999999

                    switch (data)
                    {
                        case 0x90:
                            output = 0xB0;     // 90 --> B0
                            break;
                        case 0x91:
                            output = 0x6A;     // 91 --> 6A
                            break;
                        case 0x92:
                            output = 0x6B;     // 92 --> 6B
                            break;
                        case 0x93:
                            output = 0x6C;     // 93 --> 6C
                            break;
                        case 0x94:
                            output = 0x6D;     // 94 --> 6D
                            break;
                        case 0x95:
                            output = 0x6E;     // 95 --> 6E
                            break;
                        case 0x96:
                            output = 0x6F;     // 96 --> 6F
                            break;
                        case 0x97:
                            output = 0x70;     // 97 --> 70
                            break;
                        case 0x98:
                            output = 0x71;     // 98 --> 71
                            break;
                        case 0x99:
                            output = 0x72;     // 99 --> 72
                            break;
                        case 0x9A:
                            output = 0xAA;     // 9A --> AA
                            break;
                        case 0x9B:
                            output = 0xBA;     // 9B --> BA
                            break;
                        case 0x9C:
                            output = 0xE6;     // 9C --> E6
                            break;
                        case 0x9D:
                            output = 0xB8;     // 9D --> B8
                            break;
                        case 0x9E:
                            output = 0xC6;     // 9E --> C6
                            break;
                        case 0x9F:
                            output = 0xA4;     // 9F --> A4
                            break;


                    }
                    break;

                case 0xA0: //AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA

                    switch (data)
                    {
                        case 0xA0:
                            output = 0xB5;     // A0 --> B5
                            break;
                        case 0xA1:
                            output = 0xA8;     // A1 --> A8
                            break;
                        case 0xA2:
                            output = 0x73;     // A2 --> 73
                            break;
                        case 0xA3:
                            output = 0x74;     // A3 --> 74
                            break;
                        case 0xA4:
                            output = 0x75;     // A4 --> 75
                            break;
                        case 0xA5:
                            output = 0x76;     // A5 --> 76
                            break;
                        case 0xA6:
                            output = 0x77;     // A6 --> 77
                            break;
                        case 0xA7:
                            output = 0x78;     // A7 --> 78
                            break;
                        case 0xA8:
                            output = 0x79;     // A8 --> 79
                            break;
                        case 0xA9:
                            output = 0x7A;     // A9 --> 7A
                            break;
                        case 0xAA:
                            output = 0xA1;     // AA --> A1
                            break;
                        case 0xAB:
                            output = 0xBF;     // AB --> BF
                            break;
                        case 0xAC:
                            output = 0xD0;     // AC --> D0
                            break;
                        case 0xAD:
                            output = 0xDD;     // AD --> DD
                            break;
                        case 0xAE:
                            output = 0xDE;     // AE --> DE
                            break;
                        case 0xAF:
                            output = 0xAE;     // AF --> AE
                            break;


                    }
                    break;              

                case 0xC0: //CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC

                    switch (data)
                    {
                        case 0xC0:
                            output = 0x7B;     // C0 --> 7B
                            break;
                        case 0xC1:
                            output = 0x41;     // C1 --> 41
                            break;
                        case 0xC2:
                            output = 0x42;     // C2 --> 42
                            break;
                        case 0xC3:
                            output = 0x43;     // C3 --> 43
                            break;
                        case 0xC4:
                            output = 0x44;     // C4 --> 44
                            break;
                        case 0xC5:
                            output = 0x45;     // C5 --> 45
                            break;
                        case 0xC6:
                            output = 0x46;     // C6 --> 46
                            break;
                        case 0xC7:
                            output = 0x47;     // C7 --> 47
                            break;
                        case 0xC8:
                            output = 0x48;     // C8 --> 48
                            break;
                        case 0xC9:
                            output = 0x49;     // C9 --> 49
                            break;
                        case 0xCA:
                            output = 0xAD;     // CA --> AD
                            break;
                        case 0xCB:
                            output = 0xF4;     // CB --> F4
                            break;
                        case 0xCC:
                            output = 0xF6;     // CC --> F6
                            break;
                        case 0xCD:
                            output = 0xF2;     // CD --> F2
                            break;
                        case 0xCE:
                            output = 0xF3;     // CE --> F3
                            break;
                        case 0xCF:
                            output = 0xF5;     // CF --> F5
                            break;


                    }
                    break;

                case 0xD0: //DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD

                    switch (data)
                    {
                        case 0xD0:
                            output = 0x7D;     // D0 --> 7D
                            break;
                        case 0xD1:
                            output = 0x4A;     // D1 --> 4A
                            break;
                        case 0xD2:
                            output = 0x4B;     // D2 --> 4B
                            break;
                        case 0xD3:
                            output = 0x4C;     // D3 --> 4C
                            break;
                        case 0xD4:
                            output = 0x4D;     // D4 --> 4D
                            break;
                        case 0xD5:
                            output = 0x4E;     // D5 --> 4E
                            break;
                        case 0xD6:
                            output = 0x4F;     // D6 --> 4F
                            break;
                        case 0xD7:
                            output = 0x50;     // D7 --> 50
                            break;
                        case 0xD8:
                            output = 0x51;     // D8 --> 51
                            break;
                        case 0xD9:
                            output = 0x52;     // D9 --> 52
                            break;
                        case 0xDA:
                            output = 0xB9;     // DA --> B9
                            break;
                        case 0xDB:
                            output = 0xFB;     // DB --> FB
                            break;
                        case 0xDC:
                            output = 0xFC;     // DC --> FC
                            break;
                        case 0xDD:
                            output = 0xF9;     // DD --> F9
                            break;
                        case 0xDE:
                            output = 0xFA;     // DE --> FA
                            break;
                        case 0xDF:
                            output = 0xFF;     // DF --> FF
                            break;


                    }
                    break;

                case 0xE0: //EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE

                    switch (data)
                    {
                        case 0xE0:
                            output = 0x5C;     // E0 --> 5C
                            break;
                        case 0xE1:
                            output = 0xF7;     // E1 --> F7
                            break;
                        case 0xE2:
                            output = 0x53;     // E2 --> 53
                            break;
                        case 0xE3:
                            output = 0x54;     // E3 --> 54
                            break;
                        case 0xE4:
                            output = 0x55;     // E4 --> 55
                            break;
                        case 0xE5:
                            output = 0x56;     // E5 --> 56
                            break;
                        case 0xE6:
                            output = 0x57;     // E6 --> 57
                            break;
                        case 0xE7:
                            output = 0x58;     // E7 --> 58
                            break;
                        case 0xE8:
                            output = 0x59;     // E8 --> 59
                            break;
                        case 0xE9:
                            output = 0x5A;     // E9 --> 5A
                            break;
                        case 0xEA:
                            output = 0xB2;     // EA --> B2
                            break;
                        case 0xEB:
                            output = 0xD4;     // EB --> D4
                            break;
                        case 0xEC:
                            output = 0xD6;     // EC --> D6
                            break;
                        case 0xED:
                            output = 0xD2;     // ED --> D2
                            break;
                        case 0xEE:
                            output = 0xD3;     // EE --> D3
                            break;
                        case 0xEF:
                            output = 0xD5;     // EF --> D5
                            break;


                    }
                    break;

                case 0x60: //66666666666666666666666666666666666666666666666666666666666666666

                    switch (data)
                    {
                        case 0x60:
                            output = 0x2D;     // 60 --> 2D
                            break;
                        case 0x61:
                            output = 0x2F;     // 61 --> 2F
                            break;
                        case 0x62:
                            output = 0xC2;     // 62 --> C2
                            break;
                        case 0x63:
                            output = 0xC4;     // 63 --> C4
                            break;
                        case 0x64:
                            output = 0xC0;     // 64 --> C0
                            break;
                        case 0x65:
                            output = 0xC1;     // 65 --> C1
                            break;
                        case 0x66:
                            output = 0xC3;     // 66 --> C3
                            break;
                        case 0x67:
                            output = 0xC5;     // 67 --> C5
                            break;
                        case 0x68:
                            output = 0xC7;     // 68 --> C7
                            break;
                        case 0x69:
                            output = 0x23;     // 69 --> 23
                            break;
                        case 0x6A:
                            output = 0xF1;     // 6A --> F1
                            break;
                        case 0x6B:
                            output = 0x2C;     // 6B --> 2C
                            break;
                        case 0x6C:
                            output = 0x25;     // 6C --> 25
                            break;
                        case 0x6D:
                            output = 0x5F;     // 6D --> 5F
                            break;
                        case 0x6E:
                            output = 0x3E;     // 6E --> 3E
                            break;
                        case 0x6F:
                            output = 0x3F;     // 6F --> 3F
                            break;

                    }
                    break;

                case 0x70: //77777777777777777777777777777777777777777777777777777777777777777

                    switch (data)
                    {
                        case 0x70:
                            output = 0xF8;     // 70 --> F8
                            break;
                        case 0x71:
                            output = 0xC9;     // 71 --> C9
                            break;
                        case 0x72:
                            output = 0xCA;     // 72 --> CA
                            break;
                        case 0x73:
                            output = 0xCB;     // 73 --> CB
                            break;
                        case 0x74:
                            output = 0xC8;     // 74 --> C8
                            break;
                        case 0x75:
                            output = 0xCD;     // 75 --> CD
                            break;
                        case 0x76:
                            output = 0xCE;     // 76 --> CE
                            break;
                        case 0x77:
                            output = 0xCF;     // 77 --> CF
                            break;
                        case 0x78:
                            output = 0xCC;     // 78 --> CC
                            break;
                        case 0x79:
                            output = 0x60;     // 79 --> 60
                            break;
                        case 0x7A:
                            output = 0x3A;     // 7A --> 3A
                            break;
                        case 0x7B:
                            output = 0xD1;     // 7B --> D1
                            break;
                        case 0x7C:
                            output = 0x40;     // 7C --> 40
                            break;
                        case 0x7D:
                            output = 0x27;     // 7D --> 27
                            break;
                        case 0x7E:
                            output = 0x3D;     // 7E --> 3D
                            break;
                        case 0x7F:
                            output = 0x22;     // 7F --> 22
                            break;

                    }
                    break;

                case 0x50: //55555555555555555555555555555555555555555555555555555555555555555

                    switch (data)
                    {
                        case 0x50:
                            output = 0x26;     // 50 --> 26
                            break;
                        case 0x51:
                            output = 0xE9;     // 51 --> E9
                            break;
                        case 0x52:
                            output = 0xEA;     // 52 --> EA
                            break;
                        case 0x53:
                            output = 0xEB;     // 53 --> EB
                            break;
                        case 0x54:
                            output = 0xE8;     // 54 --> E8
                            break;
                        case 0x55:
                            output = 0xED;     // 55 --> ED
                            break;
                        case 0x56:
                            output = 0xEE;     // 56 --> EE
                            break;
                        case 0x57:
                            output = 0xEF;     // 57 --> EF
                            break;
                        case 0x58:
                            output = 0xEC;     // 58 --> EC
                            break;
                        case 0x59:
                            output = 0xDF;     // 59 --> DF
                            break;
                        case 0x5A:
                            output = 0x5D;     // 5A --> 5D
                            break;
                        case 0x5B:
                            output = 0x24;     // 5B --> 24
                            break;
                        case 0x5C:
                            output = 0x2A;     // 5C --> 2A
                            break;
                        case 0x5D:
                            output = 0x29;     // 5D --> 29
                            break;
                        case 0x5E:
                            output = 0x3B;     // 5E --> 3B
                            break;
                        case 0x5F:
                            output = 0xAC;     // 5F --> AC
                            break;

                    }
                    break;

                case 0xB0: //BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB

                    switch (data)
                    {
                        case 0xB0:
                            output = 0xA2;     // B0 --> A2
                            break;
                        case 0xB1:
                            output = 0xA3;     // B1 --> A3
                            break;
                        case 0xB2:
                            output = 0xA5;     // B2 --> A5
                            break;
                        case 0xB3:
                            output = 0xB7;     // B3 --> B7
                            break;
                        case 0xB4:
                            output = 0xA9;     // B4 --> A9
                            break;
                        case 0xB5:
                            output = 0xA7;     // B5 --> A7
                            break;
                        case 0xB6:
                            output = 0xB6;     // B6 --> B6
                            break;
                        case 0xB7:
                            output = 0xBC;     // B7 --> BC
                            break;
                        case 0xB8:
                            output = 0xBD;     // B8 --> BD
                            break;
                        case 0xB9:
                            output = 0xBE;     // B9 --> BE
                            break;
                        case 0xBA:
                            output = 0x5E;     // BA --> 5E
                            break;
                        case 0xBB:
                            output = 0x21;     // BB --> 21
                            break;
                        case 0xBC:
                            output = 0xAF;     // BC --> AF
                            break;
                        case 0xBD:
                            output = 0x7E;     // BD --> 7E
                            break;
                        case 0xBE:
                            output = 0xB4;     // BE --> B4
                            break;
                        case 0xBF:
                            output = 0xD7;     // BF --> D7
                            break;


                    }
                    break;


            }

            return output;

        }


    }



}
