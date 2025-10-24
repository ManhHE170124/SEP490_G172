export default function Badge({ text, color="green"}:{text:string;color?:"green"|"gray"|"yellow"|"red"}) {
  const map:any = { green:"bg-green-100 text-green-700", gray:"bg-gray-100 text-gray-600", yellow:"bg-yellow-100 text-yellow-700", red:"bg-red-100 text-red-700" };
  return <span className={`px-3 py-1 rounded-full text-sm font-medium ${map[color]}`}>{text}</span>;
}
