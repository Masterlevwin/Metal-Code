using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Metal_Code.Models
{
    /// <summary>
    /// Представляет нарисованный пользователем профиль гибки.
    /// Профиль — это незамкнутая ломаная линия из сегментов под 90°.
    /// Каждая вершина (точка поворота) = один гиб.
    /// </summary>
    public class BendProfileDrawing : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>Точки профиля в локальных координатах канваса (мм)</summary>
        public ObservableCollection<Point> Points { get; } = new();

        /// <summary>Завершён ли рисунок (пользователь нажал "Готово")</summary>
        public bool IsFinished { get; set; }

        /// <summary>Количество сегментов</summary>
        public int SegmentsCount => Math.Max(0, Points.Count - 1);

        /// <summary>Количество гибов = количество вершин (точек поворота)</summary>
        public int BendsCount => Math.Max(0, Points.Count - 2);

        /// <summary>Суммарная длина всех сегментов (мм)</summary>
        public double TotalSegmentsLength
        {
            get
            {
                double total = 0;
                for (int i = 0; i < Points.Count - 1; i++)
                {
                    double dx = Points[i + 1].X - Points[i].X;
                    double dy = Points[i + 1].Y - Points[i].Y;
                    total += Math.Sqrt(dx * dx + dy * dy);
                }
                return total;
            }
        }

        /// <summary>Длина развертки с учетом вычета на гибы</summary>
        public double CalculateFlatLength(double bendDeduction)
        {
            if (Points.Count < 2) return 0;
            return TotalSegmentsLength - BendsCount * bendDeduction;
        }

        /// <summary>Длина сегмента по индексу</summary>
        public double GetSegmentLength(int index)
        {
            if (index < 0 || index >= Points.Count - 1) return 0;
            double dx = Points[index + 1].X - Points[index].X;
            double dy = Points[index + 1].Y - Points[index].Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Определяет, горизонтальный ли сегмент</summary>
        public bool IsSegmentHorizontal(int index)
        {
            if (index < 0 || index >= Points.Count - 1) return false;
            return Math.Abs(Points[index + 1].Y - Points[index].Y) < 0.5;
        }

        public List<(double ShelfSize, int Count, double BendLength)> GetBendInfo(double crossLength)
        {
            var result = new List<(double, int, double)>();
            if (Points.Count < 3) return result;

            // Определяем индексы сегментов-стенок (средние сегменты профиля)
            var wallIndices = GetWallIndices();

            for (int i = 0; i < BendsCount; i++)
            {
                double segBefore = GetSegmentLength(i);
                double segAfter = GetSegmentLength(i + 1);
                double shelf;

                bool beforeIsWall = wallIndices.Contains(i);
                bool afterIsWall = wallIndices.Contains(i + 1);

                if (beforeIsWall && !afterIsWall)
                {
                    // Сегмент перед гибом — стенка, полка = сегмент после гиба
                    shelf = segAfter;
                }
                else if (!beforeIsWall && afterIsWall)
                {
                    // Сегмент после гиба — стенка, полка = сегмент перед гибом
                    shelf = segBefore;
                }
                else
                {
                    // Оба сегмента не стенки (например, загиб-полка) или обе стенки.
                    // Берём короткий сегмент как полку.
                    shelf = Math.Min(segBefore, segAfter);
                }

                result.Add((Math.Round(shelf, 1), 1, crossLength));
            }
            return result;
        }

        /// <summary>
        /// Определяет индексы сегментов-стенок профиля.
        /// Стенка — средний сегмент. Для профилей с нечётным числом сегментов это один
        /// центральный сегмент, для чётных — два центральных. Для 2 сегментов (уголок)
        /// стенки нет.
        /// </summary>
        private HashSet<int> GetWallIndices()
        {
            var walls = new HashSet<int>();
            int segCount = SegmentsCount;

            if (segCount < 3) return walls;

            if (segCount % 2 == 1)
            {
                // Нечётное количество: один средний сегмент
                walls.Add((segCount - 1) / 2);
            }
            else
            {
                // Чётное количество: два средних сегмента
                walls.Add(segCount / 2 - 1);
                walls.Add(segCount / 2);
            }
            return walls;
        }

        /// <summary>Позиции линий гибки на развертке (для отрисовки)</summary>
        public List<double> GetBendPositions(double bendDeduction)
        {
            var positions = new List<double>();
            if (Points.Count < 3) return positions;

            double accumulated = 0;
            for (int i = 0; i < Points.Count - 2; i++)
            {
                accumulated += GetSegmentLength(i);
                // Позиция гибки = накопленная длина - половина вычета
                positions.Add(accumulated - bendDeduction * (i + 0.5));
            }
            return positions;
        }

        public void Clear()
        {
            Points.Clear();
            IsFinished = false;
            OnPropertyChanged(nameof(SegmentsCount));
            OnPropertyChanged(nameof(BendsCount));
        }
    }
}