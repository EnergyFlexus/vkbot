namespace OpenAi
{
	public class TimerQueue
	{
		private readonly Queue<Action> _timer_queue = new();
		private readonly object _lock_object = new();
		private readonly Timer _timer;

		public int max_queue_length {get; set;} = 250;

		private void Do(object? state)
		{
			Action action;
			lock(_lock_object)
			{
				if(_timer_queue.Count == 0) return;
				action = _timer_queue.Dequeue();
			}
			action();
		}

		public TimerQueue(long period, long due_time = 0)
		{
			_timer = new Timer(Do, null, due_time, period);
		}

		public int Add(Action action)
		{
			int length;
			lock(_lock_object)
			{
				length = _timer_queue.Count;
				if(length > max_queue_length) return -1;
				_timer_queue.Enqueue(action);
			}
			return length + 1;
		}
	}
}