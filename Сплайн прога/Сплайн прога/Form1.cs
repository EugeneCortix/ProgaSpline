using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Globalization;
using System.Windows.Forms.DataVisualization.Charting;
using static System.Collections.Specialized.BitVector32;
using System.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Xml.Linq;

namespace Сплайн_прога
{

    public partial class Form1 : Form
    {
        //область
        double X0, X1, h_X, alfa, beta, averageError, k_ejection, k_average;

        // количество разбиений
        int n_X, iter = 0;

        Random random = new Random();

        List<Mesh> _mesh = new List<Mesh>();
        List<Data> _data = new List<Data>(), _calculatedData = new List<Data>(), _erroredData = new List<Data>(), _optimizedData = new List<Data>(), _clogData = new List<Data>();

        Dictionary<string, Series> _dataOfSeries = new Dictionary<string, Series>();

        string savedata = "", filename_open ="";

        double[] q;
        public Form1()
        {
            InitializeComponent();
        }
        private List<Data> readData(string filename) //чтение из файла
        {
            string[] fileText = File.ReadAllLines(filename);
            List<Data> Data2 = new List<Data>();
            foreach (string data in fileText)
            {
                string[] datas;
                datas = data.Split('\t');
                Data2.Add(new Data() { X = double.Parse(datas[0].Replace(",", "."), CultureInfo.InvariantCulture), Y = double.Parse(datas[1].Replace(",", "."), CultureInfo.InvariantCulture), W = double.Parse(datas[2].Replace(",", "."), CultureInfo.InvariantCulture) });
            }
            return Data2;
        }
        private void Draw(List<Data> DataList, string name) //рисование в чарт
        {
            Series newSeries = new Series
            {
                ChartArea = "ChartArea1",
                XValueMember = "X",
                YValueMembers = "Y",
                Name = name
            };

            if (!_dataOfSeries.ContainsKey(name))
            {
                _dataOfSeries.Add(name, newSeries);
                _dataOfSeries[name].Points.DataBind(DataList, "X", "Y", "");
                chart1.Series.Add(_dataOfSeries[name]);
            }
            else
            {
                _dataOfSeries[name].Points.DataBind(DataList, "X", "Y", "");
            }
            foreach (Series series in chart1.Series)
            {
                series.ChartType = SeriesChartType.Point;
            }
        }
        private double psi_mas(int index, double ksi, double h)
        {
            switch (index)
            {
                case 0:
                    {
                        return phi1(ksi);
                    }
                case 1:
                    {
                        return h * phi2(ksi);
                    }
                case 2:
                    {
                        return phi3(ksi);
                    }
                case 3:
                    {
                        return h * phi4(ksi);
                    }
                default:
                    {
                        MessageBox.Show("Ошибка!!!");
                        return 0;
                    }
            }


        }
        private double phi1(double ksi)
        {
            return 1 - (3 * ksi * ksi) + (2 * ksi * ksi * ksi);
        }
        private double phi2(double ksi)
        {
            return (ksi - 2 * ksi * ksi + ksi * ksi * ksi);
        }
        private double phi3(double ksi)
        {
            return 3 * ksi * ksi - 2 * ksi * ksi * ksi;
        }
        private double phi4(double ksi)
        {
            return (-ksi * ksi + ksi * ksi * ksi);
        }
        private double[,] alfa_regular(double h) 
        {
            double[,] G = new double[4, 4] { { 36, 3 * h, -36, 3 * h }, { 3 * h, 4 * h * h, -3 * h, -h * h }, { -36, -3 * h, 36, -3 * h }, { 3 * h, -h * h, -3 * h, 4 * h * h } };
            return G;
        }
        private double[,] beta_regular(double h)
        {
            double[,] G = new double[4, 4] { { 12 / (h * h * h), 6 / (h * h), -12 / (h * h * h), 6 / (h * h)}, { 6 / (h * h), 4 / h, - 6 / (h * h), 2 / h }, { - 12 / (h * h * h), - 6 / (h * h), 12 / (h * h * h), - 6 / (h * h) }, { 6 / (h * h), 2 / h, -6 / (h * h), 4 / h } };
            return G;
        }
        private void button6_Click(object sender, EventArgs e) //сброс
        {
            for (int i = 0; i < _optimizedData.Count; i++)
            {
                _optimizedData[i].W = 1;
            }
            dataGridView5.DataSource = null;
            dataGridView5.DataSource = _optimizedData;

            iter = 0;
            label10.Text = "Количество итераций: " + iter.ToString();
        }
        private void button5_Click(object sender, EventArgs e) //засорение
        {
            int ejection;
            double clog;


            if (dataGridView2.DataSource != null) dataGridView2.DataSource = null;
            if (_clogData.Count != 0) _clogData.Clear();

            ejection = (int)numericUpDown2.Value;
            clog = (double)numericUpDown3.Value / 100;
            
            k_ejection = double.Parse(textBox5.Text);

            for (int i = 0; i < _data.Count; i++)
            {
                double r1 = random.NextDouble();

                if (r1 <= clog)
                {
                    double r2 = random.NextDouble();
                    _clogData.Add(new Data { X = _data[i].X, Y = _data[i].Y + 2 * r2 - 1, W = _data[i].W });
                }
                else
                {
                    _clogData.Add(new Data { X = _data[i].X, Y = _data[i].Y, W = _data[i].W });
                }
            }

            if (ejection != 0)
                for (int i = 0; i < ejection; i++)
                {
                    _clogData[random.Next(0, _data.Count)].Y *= k_ejection;
                }

            dataGridView2.DataSource = _clogData;

            _optimizedData = _clogData;
            dataGridView5.DataSource = _optimizedData;

            Draw(_clogData, "Clog Data");
        }
        private void button4_Click(object sender, EventArgs e) //оптимизация
        {
            if (dataGridView5.DataSource != null) dataGridView5.DataSource = null;
            for (int i = 0; i < _optimizedData.Count; i++)
            {
                if (_erroredData[i].Y >= k_average * averageError)
                {
                    _optimizedData[i].W *= 0.5;
                }
            }
            dataGridView5.DataSource = _optimizedData;
            
            iter++;

            label10.Text = "Количество итераций: " + iter.ToString();
        }
        private void button3_Click(object sender, EventArgs e) //основной расчет
        {
            n_X = ((int)numericUpDown1.Value);
            h_X = (X1 - X0) / n_X;

            if (_mesh.Count != 0)
                _mesh.Clear();

            for (int i = 0; i < n_X; i++)
            {
                _mesh.Add(new Mesh());
                _mesh[i].node_coord = X0 + i * h_X;
            }
            for (int i = 0; i < _mesh.Count; i++)
            {
                _mesh[i].datasInElemet = new List<Data>();
                for (int j = 0; j < _optimizedData.Count; j++)
                {
                    if (_optimizedData[j].X >= _mesh[i].node_coord && _optimizedData[j].X < (_mesh[i].node_coord + h_X))
                    {
                        _mesh[i].datasInElemet.Add(_optimizedData[j]);
                    }
                }
            }

            alfa = double.Parse(textBox1.Text);
            beta = double.Parse(textBox2.Text);

            SLAE_Full solve = new SLAE_Full();

            solve.resizeSLAE(2 * (n_X + 1));

            for (int i = 0; i < 2 * (n_X + 1); i++)
            {
                solve[i] = 0;
                for (int j = 0; j < 2 * (n_X + 1); j++)
                    solve[i, j] = 0;
            }
            double psi1, psi2;
            double[,] alfaRegular = alfa_regular(h_X), betaRegular = beta_regular(h_X);

            //сборка глобальной матрицы

            for (int elem = 0; elem < _mesh.Count; elem++)
            {

                //сборка локальной матрицы

                for (int i = 0; i < 4; i++)
                {
                    for (int k = 0; k < _mesh[elem].datasInElemet.Count; k++)
                    {
                        double ksi = (_mesh[elem].datasInElemet[k].X - _mesh[elem].node_coord) / h_X;
                        psi1 = psi_mas(i, ksi, h_X);
                        solve[2 * elem + i] += _mesh[elem].datasInElemet[k].W * psi1 * _mesh[elem].datasInElemet[k].Y;
                    }
                    for (int j = 0; j < 4; j++)
                    {
                        for (int k = 0; k < _mesh[elem].datasInElemet.Count; k++)
                        {
                            double ksi = (_mesh[elem].datasInElemet[k].X - _mesh[elem].node_coord) / h_X;
                            psi1 = psi_mas(i, ksi, h_X);
                            psi2 = psi_mas(j, ksi, h_X);
                            solve[2 * elem + i, 2 * elem + j] += _mesh[elem].datasInElemet[k].W * psi1 * psi2 + alfa * alfaRegular[i, j] / (30 * h_X) + beta * betaRegular[i, j];
                        }
                    }
                }
            }
            
            q = solve.solveSLAE();

            double buf = 0;
            averageError = 0;

            if (_calculatedData.Count != 0 || _erroredData.Count !=0)
            {
                _erroredData.Clear();
                _calculatedData.Clear();
                dataGridView3.DataSource = null;
                dataGridView4.DataSource = null;
            }

            for (int j = 0; j < 1000; j++)
            {
                double rnd = X0 + (X1 - X0) * random.NextDouble();
                for (int i = 0; i < n_X; i++)
                {
                    buf = 0;
                    if (rnd  >= _mesh[i].node_coord && rnd < (_mesh[i].node_coord + h_X))
                    {
                        double ksi = (rnd - _mesh[i].node_coord) / h_X;
                        for (int k = 0; k < 4; k++)
                            buf += q[2 * i + k] * psi_mas(k, ksi, h_X);
                        break;
                    }
                }
                //averageError += Math.Abs(_data[j].Y - buf);
                //_erroredData.Add(new Data { X = _data[j].X, Y = Math.Abs(_data[j].Y - buf), W = _data[j].W });
                _calculatedData.Add(new Data { X = rnd, Y = buf, W = 1 });
                savedata += rnd.ToString() + '\t' + buf.ToString() + '\n';
            }

            dataGridView3.DataSource = _calculatedData;
            //dataGridView4.DataSource = _erroredData;
            //averageError /= _data.Count;
            //textBox4.Text = averageError.ToString();

            Draw(_calculatedData, "Calculated Spline");

        }
        private void button2_Click(object sender, EventArgs e) //сохранить
        {
            saveFileDialog1.Title = "Save Data as...";
            saveFileDialog1.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
            if (saveFileDialog1.ShowDialog() == DialogResult.Cancel)
                return;
            string filename_save = saveFileDialog1.FileName;

            File.WriteAllText(filename_save, savedata);
        }
        private void button1_Click(object sender, EventArgs e) //открыть
        {
            if (_data.Count != 0) _data.Clear();

            if (_dataOfSeries.ContainsKey(filename_open))
            {
                chart1.Series.Remove(_dataOfSeries[filename_open]);
                _dataOfSeries.Remove(filename_open);
            }
            if (openFileDialog1.ShowDialog() == DialogResult.Cancel)
                return;

            filename_open = openFileDialog1.FileName;

            _data = readData(filename_open);

            X0 = _data[0].X;
            X1 = _data[_data.Count - 1].X + 0.000001;

            if (_data.Count != 0)
                button3.Enabled = true;
            dataGridView1.DataSource = _data;

            Draw(_data, filename_open);
        }
        private void textBox5_TextChanged(object sender, EventArgs e) //коэффицент выброса
        {
            if (double.TryParse(this.textBox5.Text, out k_ejection))
            {
                textBox5.BackColor = Color.White;
            }
            else
            {
                textBox5.BackColor = Color.Red;
            }

        }
        private void textBox3_TextChanged(object sender, EventArgs e) // коэффициент оптимизации
        {
            if (double.TryParse(this.textBox3.Text, out k_average))
            {
                textBox3.BackColor = Color.White;
            }
            else
            {
                textBox3.BackColor = Color.Red;
            }
        }
        private void textBox2_TextChanged(object sender, EventArgs e) //бета регуляризация чтение
        {
            if (double.TryParse(this.textBox2.Text, out beta))
            {
                textBox2.BackColor = Color.White;
            }
            else
            {
                textBox2.BackColor = Color.Red;
            }
        }
        private void textBox1_TextChanged(object sender, EventArgs e) //альфа регуляризация чтение
        {
            if (double.TryParse(this.textBox1.Text, out alfa))
            {
                textBox1.BackColor = Color.White;
            }
            else
            {
                textBox1.BackColor = Color.Red;
            }
        }
    }
    public class Data
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
    }
    public class Mesh
    {
        public double node_coord { get; set; }
        public List<Data> datasInElemet { get; set; }
    }
    public class SLAE_Full
    {
        private double[,] A;
        private double[] b;

        public SLAE_Full()
        {
            A = new double[0, 0];
            b = Array.Empty<double>();
        }

        public double this[int iInd, int jInd]
        {
            get => A[iInd, jInd];
            set => A[iInd, jInd] = value;
        }

        public double this[int iInd]
        {
            get => b[iInd];
            set => b[iInd] = value;
        }


        public SLAE_Full(int nSLAE)
        {
            A = new double[nSLAE, nSLAE];
            b = new double[nSLAE];
        }
        public void resizeSLAE(int nSLAE)
        {
            if (nSLAE == b.Length) return;
            A = new double[nSLAE, nSLAE];
            b = new double[nSLAE];
        }
        public void solveSLAE(double[] ans)
        {
            int nSLAE = b.Length;
            if (ans.Length != nSLAE)
                throw new Exception("Size of the input array is not compatable with size of SLAE");


           

            for (int i = 0; i < nSLAE; i++)
            {
                double del = A[i, i];
                double absDel = Math.Abs(del);
                int iSwap = i;


                for (int j = i + 1; j < nSLAE; j++) // ищем максимальный элемент по столбцу
                {
                    if (absDel < Math.Abs(A[j, i]))
                    {
                        del = A[j, i];
                        absDel = Math.Abs(del);
                        iSwap = j;
                    }
                }

                if (iSwap != i)
                {
                    double buf;
                    for (int j = i; j < nSLAE; j++)
                    {
                        buf = A[i, j];
                        A[i, j] = A[iSwap, j];
                        A[iSwap, j] = buf;
                    }
                    buf = b[i];
                    b[i] = b[iSwap];
                    b[iSwap] = buf;
                }

                for (int j = i; j < nSLAE; j++)
                    A[i, j] /= del;

                b[i] /= del;

                for (int j = i + 1; j < nSLAE; j++)
                {
                    if (A[j, i] == 0) continue;

                    double el = A[j, i];
                    for (int k = i; k < nSLAE; k++)
                    {
                        A[j, k] -= A[i, k] * el;
                    }

                    b[j] -= b[i] * el;
                }
            }

            for (int i = nSLAE - 1; i > -1; i--)
            {
                for (int j = i + 1; j < nSLAE; j++)
                    b[i] -= ans[j] * A[i, j];
                ans[i] = b[i];
            }
        }


        public double[] solveSLAE()
        {
            double[] ans = new double[b.Length];
            solveSLAE(ans);
            return ans;
        }

    }
}