import { createContext, useContext, useState } from "react";

const OrderContext = createContext();

export const OrderProvider = ({ children }) => {
  const [orderChanged, setOrderChanged] = useState(false);

  return (
    <OrderContext.Provider value={{ orderChanged, setOrderChanged }}>
      {children}
    </OrderContext.Provider>
  );
};

export const useOrderContext = () => useContext(OrderContext);