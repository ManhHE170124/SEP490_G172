export default function Pagination({page,total,pageSize,onChange}:{page:number;total:number;pageSize:number;onChange:(p:number)=>void}){
  const pages = Math.max(1, Math.ceil(total/pageSize));
  return (
    <div className="flex gap-2 justify-center py-3">
      {Array.from({length: pages}).map((_,i)=>
        <button key={i} onClick={()=>onChange(i+1)}
          className={`w-8 h-8 rounded border text-sm ${page===i+1?"bg-blue-600 text-white border-blue-600":"hover:bg-gray-50"}`}>
          {i+1}
        </button>
      )}
    </div>
  );
}
