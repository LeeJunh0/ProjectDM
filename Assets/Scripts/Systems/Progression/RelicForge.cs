using System;

namespace ProjectDM
{
    /// <summary>One gold stake per finished run; each of up to three independent rolls succeeds at 50%.</summary>
    internal sealed class RelicForge
    {
        private readonly SaveService save;
        private readonly Func<bool> atResult;
        private readonly Func<double> roll;
        private bool offered, attempted, finished;
        internal bool IsOpen { get; private set; }
        internal int Stake { get; private set; }
        internal int Rolls { get; private set; }
        internal bool LastSucceeded { get; private set; }
        internal int Pending => save.progress.pendingRelicFragments;
        internal int Fragments => save.progress.relicFragments;
        internal int Best => save.progress.bestRunCurrency;
        internal int Minimum => (int)(((long)Best * 4 + 4) / 5);
        internal int Gold => save.metaCurrency;
        internal bool CanContinue => IsOpen && atResult() && offered && !finished && Pending > 0 && Rolls < 3;
        internal string Message { get; private set; } = "가마에 원정 금화를 넣고 남은 마력을 추출하세요.";

        internal RelicForge(SaveService save, Func<bool> atResult, Func<double> roll = null)
        {
            this.save = save; this.atResult = atResult;
            this.roll = roll ?? new Func<double>(new System.Random().NextDouble);
            // A saved successful roll can be banked after interruption; a spent stake is never refunded.
            if (save.progress.pendingRelicFragments > 0) BankPending();
        }

        internal string BlockReason => !atResult() || !offered ? "런 결과창에서만 참가할 수 있어요." :
            attempted ? "이번 런의 제련 기회는 사용했어요." :
            Stake <= 0 ? "이번 런에서 획득한 금화가 없어요." :
            Stake < Minimum ? "이번 런 수입이 최고 기록의 80%보다 적어요." :
            Gold < Stake ? "보유 금화가 참가비보다 적어요." :
            Fragments > int.MaxValue - 5 ? "유물 조각 보관 한도에 도달했어요." : null;

        internal void RecordResult(int earned)
        {
            if (!atResult()) return;
            LeaveResult();
            Stake = Math.Max(0, earned); Rolls = 0; attempted = finished = false; offered = true;
            save.progress.bestRunCurrency = Math.Max(Best, Stake);
            Message = "금화를 녹여 마력을 유물 조각으로 굳히세요.";
            LastSucceeded = false;
            Persist();
        }

        internal bool Open()
        {
            if (!atResult() || !offered) return false;
            IsOpen = true; return true;
        }

        internal bool Begin()
        {
            if (!IsOpen || BlockReason != null) return false;
            double draw = Draw();
            attempted = true;
            save.metaCurrency -= Stake;
            Persist();
            Resolve(draw); return true;
        }

        internal bool Continue()
        {
            if (!CanContinue) return false;
            double draw = Draw();
            save.progress.pendingRelicFragments = 0;
            Persist();
            Resolve(draw); return true;
        }

        private double Draw()
        {
            double value = roll();
            if (double.IsNaN(value) || value < 0 || value >= 1) throw new InvalidOperationException("제련 추첨값 오류");
            return value;
        }

        private void Resolve(double draw)
        {
            Rolls++;
            LastSucceeded = draw < .5;
            if (LastSucceeded)
            {
                save.progress.pendingRelicFragments = Rolls == 1 ? 1 : Rolls == 2 ? 2 : 5;
                Message = "마력이 조각으로 굳었어요. 꺼내거나 더 응축하세요.";
                if (Rolls == 3) { Claim(); Message = "3회 응축 성공! 조각 5개를 꺼내 보관했어요."; }
            }
            else
            {
                save.progress.pendingRelicFragments = 0; finished = true;
                Message = "응축을 버티지 못한 조각이 부서져 연기로 흩어졌어요.";
            }
            Persist();
        }

        internal bool Claim()
        {
            if (!IsOpen || !atResult() || Pending <= 0 || finished) return false;
            int amount = Pending; BankPending(); finished = true;
            Message = "유물 조각 " + amount + "개를 가마에서 꺼내 보관했어요."; return true;
        }

        internal void Close() { if (Pending > 0) Claim(); IsOpen = false; }
        internal void LeaveResult()
        {
            if (Pending > 0) BankPending();
            IsOpen = offered = false;
        }

        private void BankPending()
        {
            save.progress.relicFragments = (int)Math.Min(int.MaxValue, (long)Fragments + Pending);
            save.progress.pendingRelicFragments = 0; Persist();
        }
        private void Persist() { save.MarkPrototypeProgressDirty(); save.FlushPrototypeProgress(); }
    }
}
