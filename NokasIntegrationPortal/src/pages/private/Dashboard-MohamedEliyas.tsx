import { useEffect, useState } from "react";

export default function Dashboard() {
  useEffect(() => {
    document.title = "Dashboard | Nokas Portal";
  }, []);

  const [registrations] = useState(2450);
  const [customers] = useState(1085);

  // Dummy bag registration list
  const lastBagOrders = [
    { BagNumber: "243324234", date: "2025-07-01", status: "Registered" },
    { BagNumber: "243324235", date: "2025-07-01", status: "In Progress" },
    { BagNumber: "243324236", date: "2025-06-30", status: "Failed" },
    { BagNumber: "243324237", date: "2025-06-30", status: "Registered" },
    { BagNumber: "243324238", date: "2025-06-29", status: "Completed" },
    { BagNumber: "243324239", date: "2025-06-28", status: "Failed" },
    { BagNumber: "243324240", date: "2025-06-28", status: "In Progress" },
    { BagNumber: "243324241", date: "2025-06-27", status: "Completed" },
    { BagNumber: "243324242", date: "2025-06-27", status: "Registered" },
    { BagNumber: "243324243", date: "2025-06-26", status: "In Progress" },
  ];

  return (
    <div className="p-4 sm:p-6 bg-gray-50 min-h-screen">
      {/* Banner */}
      <div className="mb-6 p-4 border border-yellow-300 bg-yellow-50 text-yellow-800 rounded-md">
        ⚠️ This dashboard currently shows <strong>dummy data</strong>. Development is in progress.
      </div>

      <h1 className="text-2xl font-bold mb-6 text-gray-800">Dashboard Overview</h1>

      {/* Stat Boxes */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6 mb-10">
        <div className="bg-white rounded-lg shadow border-t-4 border-blue-500 p-6">
          <h2 className="text-sm text-gray-500 mb-2">Total Registration Orders</h2>
          <p className="text-3xl font-bold text-blue-600">{registrations}</p>
        </div>

        <div className="bg-white rounded-lg shadow border-t-4 border-green-500 p-6">
          <h2 className="text-sm text-gray-500 mb-2">Total Customers</h2>
          <p className="text-3xl font-bold text-green-600">{customers}</p>
        </div>

        <div className="bg-white rounded-lg shadow border-t-4 border-purple-500 p-6">
          <h2 className="text-sm text-gray-500 mb-2">System Uptime</h2>
          <p className="text-3xl font-bold text-purple-600">99.9%</p>
        </div>
      </div>

      {/* Last 10 Bag Orders */}
      <div className="bg-white rounded-lg shadow p-6">
        <h2 className="text-xl font-semibold text-gray-700 mb-4">
          Last 10 Bag Registrations <span className="text-xs text-gray-500">[Demo Mode – Dummy Static Data]</span>
        </h2>
        <table className="w-full table-auto border-collapse">
          <thead>
            <tr className="text-left bg-gray-100 text-sm">
              <th className="p-2 border-b">Bag Number</th>
              <th className="p-2 border-b">Date</th>
              <th className="p-2 border-b">Status</th>
            </tr>
          </thead>
          <tbody>
            {lastBagOrders.map((order) => (
              <tr key={order.BagNumber} className="text-sm hover:bg-gray-50">
                <td className="p-2 border-b">{order.BagNumber}</td>
                <td className="p-2 border-b">{order.date}</td>
                <td className="p-2 border-b">
                  <span
                    className={`inline-block px-2 py-1 rounded-full text-xs font-semibold ${
                      order.status === "Completed" || order.status === "Registered"
                        ? "bg-green-100 text-green-800"
                        : order.status === "Failed"
                        ? "bg-red-100 text-red-800"
                        : "bg-yellow-100 text-yellow-800"
                    }`}
                  >
                    {order.status}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
