BinaryReader br = new BinaryReader(File.OpenRead(pathBox.Text));
            byte[] voidHead = br.ReadBytes(48);
            int level =br.ReadInt16();
            int unkownPara =br.ReadInt16();
            int rowCount = br.ReadInt16();
            byte[] voidHead2 = br.ReadBytes(10);
            for (int i = 0; i < rowCount; i++)
			{
                byte[] rowidBytes = br.ReadBytes(18);
                string rowid = Encoding.ASCII.GetString(rowidBytes);
                byte[] voidRowidHead = br.ReadBytes(6);
                double xmin = br.ReadDouble();
                double xmax = br.ReadDouble(); 
                double ymin = br.ReadDouble();
                double ymax = br.ReadDouble();
			}
            br.Close();

